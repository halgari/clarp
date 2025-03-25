namespace Clarp.Utils;

/// <summary>
/// A simple countdown latch. Threads can call wait, and once the latch is counted down to 0, all threads will be released.
/// </summary>
public class CountdownLatch
{
    private int _remain;
    private readonly EventWaitHandle _event;

    /// <summary>
    /// Create a new countdown latch with the specified count.
    /// </summary>
    public CountdownLatch(int remain)
    {
        _remain = remain;
        _event = new ManualResetEvent(false);
    }

    /// <summary>
    /// Decrement the count of the latch. If the count reaches 0, all waiting threads will be released.
    /// </summary>
    public void Signal()
    {
        if (Interlocked.Decrement(ref _remain) == 0)
            _event.Set();
    }

    /// <summary>
    /// Wait for the latch to reach 0.
    /// </summary>
    public void Wait()
    {
        _event.WaitOne();
    }

    /// <summary>
    /// Wait for the latch to reach 0, or until the specified time has passed.
    /// </summary>
    public void Wait(int lockWaitMsecs)
    {
        _event.WaitOne(lockWaitMsecs);
    }
}
