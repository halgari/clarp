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
    
    public record ForkState(bool Available, int AcquiredCount);
    
    /// <summary>
    /// Implementation of the classic dining philosophers problem using STM. The idea
    /// is that each philosopher can only eat if they can acquire both forks. If they
    /// have both forks, they eat for a short time and then release the forks. This problem
    /// is useful for testing lock-based concurrency control mechanisms because if locks
    /// are not acquired correctly, deadlocks can easily occur.
    /// </summary>
    [Test]
    public async Task CanDineAtTheDiningPhilosophersTable()
    {
        const int PhilosopherCount = 10;
        const int MaxEats = 5;
        const int EatTimeMs = 1;
        
        // These refs contain the number of times each philosopher has eaten.
        var philosopherEatCount = new Ref<int>[PhilosopherCount];
        
        // These refs contain the state of the forks. If a fork is available, the value is true.
        var forks = new Ref<ForkState>[PhilosopherCount];
        
        for (var i = 0; i < PhilosopherCount; i++)
        {
            forks[i] = new Ref<ForkState>(new ForkState(true, 0));
            philosopherEatCount[i] = new Ref<int>(0);
        }

        var philosophers = new Task[PhilosopherCount];
        for (var i = 0; i < PhilosopherCount; i++)
        {
            var philosopherId = i;
            philosophers[i] = Task.Run(() =>
            {
                // Get both fork indices
                var leftFork = philosopherId;
                var rightFork = (philosopherId + 1) % PhilosopherCount;

                var eats = 0;
                while (eats < MaxEats)
                {
                    // First try to acquire both forks
                    var canEat = LockingTransaction.RunInTransaction(() =>
                    {
                        var leftForkState = forks[leftFork].Value;
                        var rightForkState = forks[rightFork].Value;
                        // If either fork is not available, return false
                        if (!leftForkState.Available || !rightForkState.Available)
                            return false;
                        
                        // Otherwise, acquire both forks, and increment the eat/aquired count
                        forks[leftFork].Value = new ForkState(false, leftForkState.AcquiredCount + 1);
                        forks[rightFork].Value = new ForkState(false, rightForkState.AcquiredCount + 1);
                        philosopherEatCount[philosopherId].Value++;
                        return true;
                    });

                    if (!canEat)
                    {
                        continue;
                    }
                    
                    eats++;
                    Thread.Sleep(EatTimeMs);
                    
                    // Now return the forks
                    LockingTransaction.RunInTransaction(() =>
                    {
                        forks[leftFork].Value = forks[leftFork].Value with { Available = true };
                        forks[rightFork].Value = forks[rightFork].Value with { Available = true };
                        return true;
                    });
                }
            });
        }

        await Task.WhenAll(philosophers);
        
        for (var i = 0; i < PhilosopherCount; i++)
        {
            // Each philosopher should have released their forks
            await Assert.That(forks[i].Value.Available).IsTrue();
            // Each philosopher needs both forks to eat, so the aquired count should be twice the eat count
            await Assert.That(forks[i].Value.AcquiredCount).IsEqualTo(MaxEats * 2);
            // Each philosopher should have eaten the maximum number of times
            await Assert.That(philosopherEatCount[i].Value).IsEqualTo(MaxEats);
        }
    }
}