namespace Clarp.Concurrency;

public class Notify
{
    public Notify(ARefBase r, object oldValue, object newValue)
    {
        Ref = r;
        OldValue = oldValue;
        NewValue = newValue;
    }

    public ARefBase Ref { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }
}