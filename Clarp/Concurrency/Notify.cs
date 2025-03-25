namespace Clarp.Concurrency;

public class Notify
{
    public Ref Ref { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }

    public Notify(Ref r, object oldValue, object newValue)
    {
        Ref = r;
        OldValue = oldValue;
        NewValue = newValue;
    }

}
