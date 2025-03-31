using System.Collections.Immutable;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Clarp.Abstractions;
using Clarp.Utils;

namespace Clarp.Concurrency;

internal static class RefGlobals
{
    private static long _nextId;

    public static long NextId()
    {
        return Interlocked.Increment(ref _nextId);
    }
}

public abstract class ARefBase : IComparable<ARefBase>
{
    /// <summary>
    /// A unique used to resolve contentions between locks. When all the refs in a transaction need to be locked at once
    /// they can be locked in order of their ID, in order to avoid deadlocks.
    /// </summary>
    private readonly long _id = RefGlobals.NextId();
    
    protected readonly ReaderWriterLockSlim Lock = new();
    internal LockingTransaction.Info? TransactionInfo = null;

    /// <summary>
    /// The transaction values for this reference. This is a circular linked list of slots, each with a read point.
    /// that specifies the point at which the value was valid.
    /// </summary>
    internal ATVal? TVals = null;

    object? CurrentTVal { get; }
    bool HasWatches { get; }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void EnterReadLock() => Lock.EnterReadLock();
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void ExitReadLock() => Lock.ExitReadLock();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void EnterWriteLock() => Lock.EnterWriteLock();

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal void ExitWriteLock() => Lock.ExitWriteLock();
    
    /// <summary>
    /// Try to enter the write lock within the given time limit.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal bool TryEnterWriteLock(int lockWaitMsecs) => Lock.TryEnterWriteLock(lockWaitMsecs);


    public LockingTransaction.Info? ThrowIfTValPointOver(long readPoint)
    {
        EnterReadLock();
        if (TVals != null && TVals!.ReadPoint > readPoint)
        {
            ExitReadLock();
            throw LockingTransaction.RETRY_EX;
        }
        return TransactionInfo;
    }

    internal abstract class ATVal
    {
        /// <summary>
        /// A reference to the next transaction value in the list, if any.
        /// </summary>
        public ATVal? Next;
        
        /// <summary>
        /// The read point of the transaction value.
        /// </summary>
        public long ReadPoint;
        
        /// <summary>
        /// A reference to the previous transaction value in the list
        /// </summary>
        public ATVal Prior;
    }
    
    /// <summary>
    /// Returns true, if the current TVals read point is greater than the given read point.
    /// </summary>
    public bool TValsOverPoint(long readPoint)
    {
        return TVals != null && TVals.ReadPoint > readPoint;
    }
    
    /// <summary>
    /// Commits the value to the reference. This is called by the transaction when it is ready to commit and all the
    /// associated locks are aquired.
    /// </summary>
    internal abstract void CommitValue(LockingTransaction tx, object? value, long commitPoint);

    /// <summary>
    /// Compares this reference to another reference based on their IDs. This is used to resolve contention between the locks
    /// in both refs.
    /// </summary>
    public int CompareTo(ARefBase? other)
    {
        return other == null ? 1 : _id.CompareTo(other._id);
    }

    protected int HistCount()
    {
        if (TVals == null)
            return 0;

        var count = 0;
        for (var tval = TVals.Next; tval != TVals; tval = tval!.Next)
            count++;
        return count;
    }
    
    internal abstract void NotifyWatchers(object? oldValue, object? newValue);
}

public class Ref<T> : ARefBase, IRef<T>
{
    internal int faults;
    private ImmutableDictionary<object, IRef<T>.WatchFn> _watchers =
        ImmutableDictionary<object, IRef<T>.WatchFn>.Empty;
    
    public Ref(T initialValue)
    {
        TVals = new TVal(initialValue, 0);
        faults = 0;
    }

    public Ref()
    {
        TVals = null;
        faults = 0;
    }

    public static int MinHistory { get; set; } = 0;
    public static int MaxHistory { get; set; } = 10;

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

    internal override void CommitValue(LockingTransaction tx, object? value, long commitPoint)
    {
        var oldVal = TVals == null ? default : ((TVal)TVals).Value;
        var hCount = HistCount();
        if (TVals == null)
        {
            TVals = new TVal((T)value!, commitPoint);
        }
        else if ((faults > 0 && hCount < MaxHistory) || hCount < MinHistory)
        {
            TVals = new TVal((T)value!, commitPoint, (TVal)TVals);
            faults = 0;
        }
        else
        {
            TVals = TVals.Next;
            ((TVal)TVals!).Value = (T)value!;
            TVals!.ReadPoint = commitPoint;
        }

        tx.AddPostCommit(this, oldVal, value);
    }

    internal override void NotifyWatchers(object? oldValue, object? newValue)
    {
        if (oldValue == newValue)
            return;

        foreach (var (key, watcher) in _watchers)
        {
            watcher(key, this, (T)oldValue, (T)newValue);
        }
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
            Lock.EnterReadLock();
            if (TVals != null)
                return ((TVal)TVals).Value!;
            throw new InvalidOperationException("Ref is unbound");
        }
        finally
        {
            Lock.ExitReadLock();
        }
    }

    public bool TryGetTVal(long readPoint, out T value)
    {
        try
        {
            Lock.EnterReadLock();
            var ver = TVals;
            if (ver == null)
                throw new InvalidOperationException(this + " is unbound");
            do
            {
                if (ver.ReadPoint <= readPoint)
                {
                    value = ((TVal)ver).Value!;
                    return true;
                }
            } while ((ver = ver.Prior) != TVals);
        }
        finally
        {
            Lock.ExitReadLock();
        }

        value = default!;
        return false;
    }

    /// <summary>
    ///     Container for transaction values
    /// </summary>
    internal class TVal : ATVal
    {
        public T? Value;

        public TVal(T? val, long readPoint, TVal prior)
        {
            this.Value = val;
            this.ReadPoint = readPoint;
            this.Prior = prior;
            Next = prior.Next;
            this.Prior.Next = this;
            Next!.Prior = this;
        }

        public TVal(T? val, long point)
        {
            this.Value = val;
            this.ReadPoint = point;
            Prior = this;
            Next = this;
        }
    }
    
    public IRef<T>.ValidatorFn? Validator { get; set; }
    public IRef<T> AddWatch(object key, IRef<T>.WatchFn watchFn)
    {
        do
        {
            var oldWatches = _watchers;
            var newWatches = oldWatches.Add(key, watchFn);
            if (Atomic.CompareAndSet(ref _watchers, oldWatches, newWatches))
                return this;
        } while (true);
    }

    public IRef<T> RemoveWatch(object key)
    {
        throw new NotImplementedException();
    }

    public IReadOnlyDictionary<object, IRef<T>.WatchFn> Watches { get; }
}