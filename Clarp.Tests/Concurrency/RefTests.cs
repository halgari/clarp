using Clarp.Concurrency;

namespace Clarp.Tests.Concurrency;

public class RefTests
{
    [Test]
    public async Task CanUpdateRefs()
    {
        var refA = new Ref<int>();
        var refB = new Ref<int>();

        var txResult = LockingTransaction.RunInTransaction(() =>
        {
            refA.Value = 1;
            refB.Value = 2;
            return 42;
        });
        
        await Assert.That(txResult).IsEqualTo(42);

        await Assert.That(refA.Value).IsEqualTo(1);
        await Assert.That(refB.Value).IsEqualTo(2);
    }

    [Test]
    public async Task ConsistencyIsMaintainedWithParallelAccess()
    {
        const int MaxValue = 10000;
        const int ThreadCount = 10;
        
        var refA = new Ref<int>(MaxValue);
        var refB = new Ref<int>(0);
        
        var tasks = new Task[ThreadCount];
        for (var i = 0; i < ThreadCount; i++)
        {
            tasks[i] = Task.Run(ProcessFn);
        }
        
        await Task.WhenAll(tasks);
        
        await Assert.That(refA.Value).IsEqualTo(0);
        await Assert.That(refB.Value).IsEqualTo(MaxValue);
        
        


        void ProcessFn()
        {
            while (true)
            {
                var done = LockingTransaction.RunInTransaction(() =>
                {
                    if (refA.Value == 0)
                        return true;

                    refA.Value--;
                    refB.Value++;
                    return false;
                });
                if (done)
                    break;
            }
        }
    }
}