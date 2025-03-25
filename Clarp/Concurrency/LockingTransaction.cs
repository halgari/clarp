using Clarp.Utils;

namespace Clarp.Concurrency;

public class LockingTransaction
{
    public const int RETRY_LIMIT = 10000;
    public const int LOCK_WAIT_MSECS = 100;
    public const long BARGE_WAIT_NANOS = 10 * 1000000;

    internal static RetryEx RETRY_EX = new();

    public enum State : int
    {
        RUNNING,
        COMITTING,
        RETRY,
        KILLED,
        COMITTED,
    }

    private static readonly ThreadLocal<LockingTransaction> transaction = new();
    private static long _lastPoint;

    #region Instance Fields

    Info? _info;
    private long _readPoint;
    private long _startPoint;
    private long _startTime;

    private Dictionary<IGenericRef, object?> _vals = new();
    private HashSet<IGenericRef> _sets = new();
    private readonly SortedDictionary<IGenericRef, List<object>> _commutes = new();
    private readonly HashSet<IGenericRef> _ensures = [];

    #endregion


    internal class RetryEx : Exception;

    class AbortException : Exception;

    public class Info
    {
        public int _status;

        public State Status => (State)_status;

        public long StartPoint;
        public readonly CountdownLatch Latch;

        public Info(State status, long startPoint)
        {
            _status = (int)status;
            StartPoint = startPoint;
            Latch = new CountdownLatch(1);
        }

        public bool Running => Status is State.RUNNING or State.COMITTING;
    }

    void GetReadPoint()
        => _readPoint = Interlocked.Increment(ref _lastPoint);

    long GetCommitPoint()
        => Interlocked.Increment(ref _lastPoint);

    void Stop(State status)
    {
        if (_info is null)
            return;

        lock (_info)
        {
            _info._status = (byte)status;
            _info.Latch.Signal();
        }
        _info = null;
        _vals.Clear();
        _sets.Clear();
        _commutes.Clear();
    }

    void TryWriteLock(IGenericRef r)
    {
        try
        {
            if (!r.TryEnterWriteLock(LOCK_WAIT_MSECS))
                throw RETRY_EX;
        }
        catch (ThreadInterruptedException)
        {
            throw RETRY_EX;
        }
    }

    private void ReleaseIfEnsured(IGenericRef r)
    {
        if (!_ensures.Contains(r))
            return;

        _ensures.Remove(r);
        r.ExitReadLock();
    }

    private void Abort()
    {
        Stop(State.KILLED);
        throw new AbortException();
    }

    private bool BargeTimeElapsed => (DateTime.Now.Ticks - _startTime) > BARGE_WAIT_NANOS;

    private bool Barge(Info refInfo)
    {
        var barged = false;
        if (BargeTimeElapsed && _startPoint < refInfo.StartPoint)
        {
            barged = Interlocked.CompareExchange(ref refInfo._status, (int)State.KILLED, (int)State.RUNNING) == (int)State.RUNNING;
            if (barged)
                refInfo.Latch.Signal();
        }
        return barged;
    }

    void Lock(IGenericRef r)
    {
        ReleaseIfEnsured(r);
        var unlocked = true;
        try
        {
            TryWriteLock(r);
            unlocked = false;

            if (r.TValsOverPoint(_readPoint))
                throw RETRY_EX;

            var refInfo = r.TransactionInfo;

            // write lock conflict
            if (refInfo != null && refInfo != _info && refInfo.Running)
            {
                if (!Barge(refInfo))
                {
                    r.ExitWriteLock();
                    unlocked = true;
                    BlockAndBail(refInfo!);
                    return;
                }
            }

            r.TransactionInfo = _info!;
            return;
            //return r.tvals?.val;
        }
        finally
        {
            if (!unlocked)
                r.ExitWriteLock();
        }
    }

    object? BlockAndBail(Info refInfo)
    {
        Stop(State.RETRY);
        try
        {
            refInfo.Latch.Wait(LOCK_WAIT_MSECS);
        }
        catch (ThreadInterruptedException ex)
        {
            // ignore
        }
        throw RETRY_EX;
    }

    public T Run<T>(Func<T> action)
    {
        bool done = false;
        T ret = default!;

        List<IGenericRef> locked = [];
        List<Notify> notify = [];

        for (var i = 0; i < RETRY_LIMIT; i++)
        {
            try
            {
                GetReadPoint();
                if (i == 0)
                {
                    _startPoint = _readPoint;
                    _startTime = DateTime.Now.Ticks;
                }

                _info = new Info(State.RUNNING, _startPoint);
                ret = action();

                // Make sure we're not killed before we start the commit
                if (Interlocked.CompareExchange(ref _info._status, (int)State.COMITTING, (int)State.RUNNING) ==
                    (int)State.RUNNING)
                {
                    // Handle commutes
                    foreach (var (r, commutes) in _commutes)
                    {
                        if (_sets.Contains(r))
                            continue;

                        var wasEnsured = _ensures.Contains(r);
                        ReleaseIfEnsured(r);
                        TryWriteLock(r);
                        locked.Add(r);

                        if (wasEnsured && r.TValsOverPoint(_readPoint))
                            throw RETRY_EX;

                        var refInfo = r.TransactionInfo;
                        if (refInfo != null && refInfo != _info && refInfo.Running)
                        {
                            if (!Barge(refInfo))
                                throw RETRY_EX;
                        }

                        throw new NotImplementedException();
                        /*
                        var val = r.TVal;
                        _vals[r] = val;

                        foreach (var cfn in commutes)
                        {
                            var nVal = ((Func<object?, object?>)cfn)(val);
                            _vals[r] = nVal;
                        }
                        */
                    }

                    // Handle sets
                    foreach (var r in _sets)
                    {
                        TryWriteLock(r);
                        locked.Add(r);
                    }

                    // TODO: Notifications

                    // At this point, all values are calculated and refs can now be written
                    var commitPoint = GetCommitPoint();
                    foreach (var (r, value) in _vals)
                    {
                        r.CommitValue(value, commitPoint);
                        // TODO: notify watches
                    }

                    done = true;
                    _info._status = (int)State.COMITTED;
                }
            }
            catch (RetryEx ex)
            {
                // Eat it, we'll retry below
            }
            finally
            {
                foreach (var l in locked)
                    l.ExitWriteLock();
                locked.Clear();

                foreach (var r in _ensures)
                    r.ExitReadLock();
                _ensures.Clear();

                Stop(done ? State.COMITTED : State.RETRY);
                try
                {
                    if (done)
                    {
                        // TODO: notify watches
                    }
                }
                finally
                {
                    notify.Clear();
                    // TODO: Actions after commit
                }
            }
        }
        if (!done)
            throw new Exception("Transaction retry limit reached");
        return ret;
    }
    
    public static T RunInTransaction<T>(Func<T> action)
    {
        var tx = LockingTransaction.Current;
        T ret;
        if (tx == null)
        {
            tx = new LockingTransaction();
            transaction.Value = tx;
            try
            {
                ret = tx.Run(action);
            }
            finally
            {
                transaction.Value = null;
            }
        }
        else
        {
            if (tx._info != null)
            {
                ret = action();
            }
            else
            {
                ret = tx.Run(action);
            }
        }

        return ret;
    }

    public T DoGet<T>(Ref<T> r)
    {
        if (!_info!.Running)
            throw RETRY_EX;
        if (_vals.TryGetValue(r, out var val))
        {
            return (T)val!;
        }

        if (r.TryGetTVal(_readPoint, out var result))
            return result;
        
        Interlocked.Increment(ref r.faults);
        throw RETRY_EX;
    }

    public object? DoSet(IGenericRef r, object? val)
    {
        if (!_info!.Running)
            throw RETRY_EX;
        if (_commutes.ContainsKey(r))
            throw new InvalidOperationException("Can't set after commute");
        if (_sets.Add(r))
            Lock(r);
        _vals[r] = val;
        return val;
    }

    public void DoEnsure(IGenericRef r)
    {
        if (!_info!.Running)
            throw RETRY_EX;

        if (_ensures.Contains(r))
            return;

        var refInfo = r.ThrowIfTValPointOver(_readPoint);

        if (refInfo is { Running: true })
        {
            r.ExitReadLock();
            if (refInfo != _info)
                BlockAndBail(refInfo);
        }
        else
        {
            _ensures.Add(r);
        }

    }

    public static LockingTransaction? Current => transaction.Value;

    public static LockingTransaction GetOrException()
    {
        var tx = Current;
        if (tx != null)
            return tx;
        throw new InvalidOperationException("No transaction in scope");
        
    }
}
