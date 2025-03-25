using Clarp.Utils;

namespace Clarp.Concurrency;

public class LockingTransaction
{
    public const int RETRY_LIMIT = 10000;
    public const int LOCK_WAIT_MSECS = 100;
    public const long BARGE_WAIT_NANOS = 10 * 1000000;

    private static RetryEx RETRY_EX = new();

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

    private Dictionary<Ref, object?> _vals = new();
    private HashSet<Ref> _sets = new();
    private SortedDictionary<Ref, List<object>> _commutes = new();
    private readonly HashSet<Ref> _ensures = [];

    #endregion


    class RetryEx : Exception;

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

    void TryWriteLock(Ref r)
    {
        try
        {
            if (!r.Lock.TryEnterWriteLock(LOCK_WAIT_MSECS))
                throw RETRY_EX;
        }
        catch (ThreadInterruptedException)
        {
            throw RETRY_EX;
        }
    }

    private void ReleaseIfEnsured(Ref r)
    {
        if (!_ensures.Contains(r))
            return;

        _ensures.Remove(r);
        r.Lock.ExitReadLock();
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

    object? Lock(Ref r)
    {
        ReleaseIfEnsured(r);
        var unlocked = true;
        try
        {
            TryWriteLock(r);
            unlocked = false;

            if (r.tvals != null && r.tvals.point > _readPoint)
                throw RETRY_EX;

            var refInfo = r.tinfo;

            // write lock conflict
            if (refInfo != null && refInfo != _info && refInfo.Running)
            {
                if (!Barge(refInfo))
                {
                    r.Lock.ExitWriteLock();
                    unlocked = true;
                    return BlockAndBail(refInfo);
                }
            }

            r.tinfo = _info!;
            return r.tvals?.val;
        }
        finally
        {
            if (!unlocked)
                r.Lock.ExitWriteLock();
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

        List<Ref> locked = [];
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
                    foreach (var (r, eVal) in _commutes)
                    {
                        if (_sets.Contains(r))
                            continue;

                        var wasEnsured = _ensures.Contains(r);
                        ReleaseIfEnsured(r);
                        TryWriteLock(r);
                        locked.Add(r);

                        if (wasEnsured && r.tvals != null && r.tvals.point > _readPoint)
                            throw RETRY_EX;

                        var refInfo = r.tinfo;
                        if (refInfo != null && refInfo != _info && refInfo.Running)
                        {
                            if (!Barge(refInfo))
                                throw RETRY_EX;
                        }

                        var val = r.tvals?.val;
                        _vals[r] = val;

                        foreach (var cfn in eVal)
                        {
                            var nVal = ((Func<object?, object?>)cfn)(val);
                            _vals[r] = nVal;
                        }
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
                        var oldVal = r.tvals?.val;
                        int hCount = r.HistCount();

                        if (r.tvals == null)
                            r.tvals = new Ref.TVal(value, commitPoint);
                        else if ((r.faults > 0) && hCount < r.MaxHistory || hCount < r.MinHistory)
                        {
                            r.tvals = new Ref.TVal(value, commitPoint, r.tvals);
                            r.faults = 0;
                        }
                        else
                        {
                            r.tvals = r.tvals.next;
                            r.tvals!.val = value;
                            r.tvals!.point = commitPoint;
                        }

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
                    l.Lock.ExitWriteLock();
                locked.Clear();

                foreach (var r in _ensures)
                    r.Lock.ExitReadLock();
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

    public object? DoGet(Ref r)
    {
        if (!_info!.Running)
            throw RETRY_EX;
        if (_vals.TryGetValue(r, out var val))
        {
            return val!;
        }

        try
        {
            r.Lock.EnterReadLock();
            if (r.tvals == null)
                throw new InvalidOperationException(r + " is unbound");
            var ver = r.tvals;
            do
            {
                if (ver.point <= _readPoint)
                    return ver.val;
            } while ((ver = ver.prior) != r.tvals);
        }
        finally
        {
            r.Lock.ExitReadLock();
        }

        // No previous value exists
        Interlocked.Increment(ref r.faults);
        throw RETRY_EX;
    }

    public object? DoSet(Ref r, object? val)
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

    public void DoEnsure(Ref r)
    {
        if (!_info!.Running)
            throw RETRY_EX;

        if (_ensures.Contains(r))
            return;

        r.Lock.EnterReadLock();
        if (r.tvals == null && r.tvals!.point > _readPoint)
        {
            r.Lock.ExitReadLock();
            throw RETRY_EX;
        }

        var refInfo = r.tinfo;

        if (refInfo is { Running: true })
        {
            r.Lock.ExitReadLock();
            if (refInfo != _info)
                BlockAndBail(refInfo);
        }
        else
        {
            _ensures.Add(r);
        }

    }

    public static LockingTransaction Current =>
        transaction.Value ?? throw new InvalidOperationException("No transaction running");

}
