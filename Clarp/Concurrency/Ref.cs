namespace Clarp.Concurrency;

public class Ref
{
    public readonly ReaderWriterLockSlim Lock = new(LockRecursionPolicy.SupportsRecursion);

    public int MinHistory { get; set; }
    public int MaxHistory { get; set; }

    public int faults;

    /// <summary>
    /// Container for transaction values
    /// </summary>
    public class TVal
    {
        public Object? val;
        public long point;
        public TVal prior;
        public TVal? next;

        public TVal(Object? val, long point, TVal prior)
        {
            this.val = val;
            this.point = point;
            this.prior = prior;
            next = this.prior.next;
            prior.next = this;
            next!.prior = this;
        }

        public TVal(Object? val, long point)
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
        for (var tval = tvals; tval != tvals; tval = tval!.next)
            count++;
        return count;
    }

    public object? Alter(Func<object?, object?> alterFunc)
    {
        var t = LockingTransaction.Current;
        return t.DoSet(this, alterFunc(t.DoGet(this)));
    }

    public object? Value
    {
        get => LockingTransaction.Current.DoGet(this);
        set => LockingTransaction.Current.DoSet(this, value);
    }
}
