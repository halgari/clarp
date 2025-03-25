namespace Clarp.Concurrency;

public class Notify
{
    public IGenericRef Ref { get; set; }
    public object OldValue { get; set; }
    public object NewValue { get; set; }

    public Notify(IGenericRef r, object oldValue, object newValue)
    {
        Ref = r;
        OldValue = oldValue;
        NewValue = newValue;
    }

}
