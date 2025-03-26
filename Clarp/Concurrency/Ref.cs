namespace Clarp.Concurrency;

internal static class RefGlobals
{
    private static long _nextId;
    
    public static long NextId()
    {
        return Interlocked.Increment(ref _nextId);
    }
}

public interface IGenericRef : IComparable<IGenericRef>
{
    void EnterReadLock();
    void ExitReadLock();
    
    void EnterWriteLock();
    
    void ExitWriteLock();

    LockingTransaction.Info? ThrowIfTValPointOver(long readPoint);
    bool TValsOverPoint(long readPoint);
    LockingTransaction.Info? TransactionInfo { get; set; }
    object? CurrentTVal { get; }
    bool TryEnterWriteLock(int lockWaitMsecs);
    void CommitValue(object? value, long commitPoint);
}
public class Ref<T> : IGenericRef
{
    public readonly ReaderWriterLockSlim _lock = new(LockRecursionPolicy.SupportsRecursion);

    public static int MinHistory { get; set; }
    public static int MaxHistory { get; set; }

    public int faults;
    
    private readonly long _id = RefGlobals.NextId();

    public Ref(T initialValue)
    {
        tvals = new TVal(initialValue, 0);
        faults = 0;
    }

    public Ref()
    {
        tvals = null;
        faults = 0;
    }
    
    /// <summary>
    /// Container for transaction values
    /// </summary>
    public class TVal
    {
        public T? val;
        public long point;
        public TVal prior;
        public TVal? next;

        public TVal(T? val, long point, TVal prior)
        {
            this.val = val;
            this.point = point;
            this.prior = prior;
            next = prior.next;
            this.prior.next = this;
            this.next!.prior = this;
        }

        public TVal(T? val, long point)
        {
            this.val = val;
            this.point = point;
            prior = this;
            next = this;
        }
    }

    public TVal? tvals;

    public LockingTransaction.Info? tinfo;

    public int HistCount()
    {
        if (tvals == null)
            return 0;

        int count = 0;
        for (var tval = tvals.next; tval != tvals; tval = tval!.next)
            count++;
        return count;
    }

    public object? Alter(Func<object?, object?> alterFunc)
    {
        var t = LockingTransaction.Current;
        return t.DoSet(this, alterFunc(t.DoGet(this)));
    }

    private T GetCurrentValue()
    {
        try
        {
            _lock.EnterReadLock();
            if (tvals != null)
                return (T)tvals.val;
            throw new InvalidOperationException("Ref is unbound");
        }
        finally
        {
            _lock.ExitReadLock();
        }
    }

    public T Value
    {
        get
        {
            var t = LockingTransaction.Current;
            if (t == null)
                return GetCurrentValue();
            return (T)t.DoGet(this)!;
        }
        set => LockingTransaction.GetOrException().DoSet(this, value);
    }

    public void EnterReadLock()
    {
        _lock.EnterReadLock();
    }

    public void ExitReadLock()
    {
        _lock.ExitReadLock();
    }

    public void EnterWriteLock()
    {
        _lock.EnterWriteLock();
    }

    public void ExitWriteLock()
    {
        _lock.ExitWriteLock();
    }

    public LockingTransaction.Info? ThrowIfTValPointOver(long readPoint)
    {
        _lock.EnterReadLock();
        if (tvals != null && tvals!.point > readPoint)
        {
            _lock.ExitReadLock();
            throw LockingTransaction.RETRY_EX;
        }
        return tinfo;
    }

    public bool TValsOverPoint(long readPoint)
    {
        return tvals != null && tvals.point > readPoint;
    }

    public LockingTransaction.Info? TransactionInfo
    {
        get { return tinfo; }
        set { tinfo = value; }
    }

    public object? CurrentTVal
    {
        get { 
            if (tvals == null)
                return null;
            return tvals.val;
        }
    }

    public bool TryEnterWriteLock(int lockWaitMsecs)
    {
        return _lock.TryEnterWriteLock(lockWaitMsecs);
    }

    public void CommitValue(object? value, long commitPoint)
    {
        //var oldVal = tvals?.val;
        var hCount = HistCount();

        if (tvals == null)
            tvals = new TVal((T)value, commitPoint);
        else if ((faults > 0) && hCount < MaxHistory || hCount < MinHistory)
        {
            tvals = new TVal((T)value, commitPoint, tvals);
            faults = 0;
        }
        else
        {
            tvals = tvals.next;
            tvals!.val = (T)value!;
            tvals!.point = commitPoint;
        }
    }

    public bool TryGetTVal(long readPoint, out T value)
    {
        try
        {
            _lock.EnterReadLock();
            var ver = tvals;
            if (ver == null)
                throw new InvalidOperationException(this + " is unbound");
            do
            {
                if (ver.point <= readPoint)
                {
                    value = ver.val!;
                    return true;
                }
            } while ((ver = ver.prior) != tvals);
        }
        finally
        {
            _lock.ExitReadLock();
        }
        value = default!;
        return false;
    }

    public int CompareTo(IGenericRef? other) 
        => other is null ? 1 : _id.CompareTo(((Ref<T>)other)._id);
}
