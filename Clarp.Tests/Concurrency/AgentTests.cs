using System.Collections.Immutable;
using Clarp.Concurrency;
using TUnit.Assertions.Enums;

namespace Clarp.Tests.Concurrency;

public class AgentTests
{
    private const int WaitTime = 100;
    [Test]
    public async Task CanEnqueueItems()
    {
        var agent = new Agent<int>();

        for (var i = 0; i < 10; i++)
        {
            agent.Send(s => s + 1);
        }
        Thread.Sleep(WaitTime);
        
        await Assert.That(agent.Value).IsEqualTo(10);
    }

    [Test]
    public async Task CanEneuqueItemsInParallel()
    {
        const int TaskCount = 10000;
        var agent = new Agent<int>();
        
        var tasks = new List<Task>();
        for (var i = 0; i < TaskCount; i++)
        {
            tasks.Add(Task.Run(() => agent.Send(s => s + 1)));
        }
        
        await Task.WhenAll(tasks);
        Thread.Sleep(WaitTime);
        
        await Assert.That(agent.Value).IsEqualTo(TaskCount);
    }

    /// <summary>
    /// Items should be processed in the order they are enqueued.
    /// </summary>
    [Test]
    public async Task TasksMaintainOrdering()
    {
        const int TaskCount = 10000;
        
        var agent = new Agent<ImmutableList<int>>( ImmutableList<int>.Empty);
        
        for (var i = 0; i < TaskCount; i++)
        {
            var v = i;
            agent.Send(s => s.Add(v));
        }
        
        Thread.Sleep(WaitTime);
        
        await Assert.That(agent.Value.ToList()).IsEquivalentTo(Enumerable.Range(0, TaskCount).ToList());
    }

    [Test]
    public async Task CanWatchAgents()
    {
        var updates = new List<(int From, int To)>();
        
        var agent = new Agent<int>(0);
        
        agent.AddWatch(42, (k, r, o, n) => {updates.Add((o, n));});

        agent.Send(s => s + 1);
        agent.Send(s => s + 1);
        
        Thread.Sleep(WaitTime);
        
        await Assert.That(updates).IsEquivalentTo(new List<(int From, int To)> {(0, 1), (1, 2)});

        agent.RemoveWatch(42);

        await Assert.That(agent.Watches).IsEmpty();
    }

    public record ForwardingState(Agent<ForwardingState>? Next, int Count, string Name);
    
    [Test]
    public async Task AgentsCanSendToAgents()
    {
        const int MaxCount = 1000;
        
        // Each agent will forward the message to the next agent in the chain
        var a = new Agent<ForwardingState>(new ForwardingState(null, 0, "A"));
        var b = new Agent<ForwardingState>(new ForwardingState(a, 0, "B"));
        var c = new Agent<ForwardingState>(new ForwardingState(b, 0, "C"));

        // Close the loop
        a.Send(s => s with { Next = c });
        
        // Once they are done, they will each send their name to the finished agent
        var finished = new Agent<ImmutableList<string>>(ImmutableList<string>.Empty);
        
        // Start the chain
        a.Send(Handle);
        
        Thread.Sleep(WaitTime);
        
        await Assert.That(finished.Value.ToList()).IsEquivalentTo(new List<string> {"A", "B", "C"}, CollectionOrdering.Any);
        return;


        ForwardingState Handle(ForwardingState state)
        {
            if (state.Next == null)
                return state;
            
            if (state.Count == MaxCount)
            {
                finished.Send(s => s.Add(state.Name));
                state.Next.Send(Handle);
                return state with { Next = null!};
            }
            state.Next!.Send(Handle);
            return state with { Count = state.Count + 1 };
        }
    }
}