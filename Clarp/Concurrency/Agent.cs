using System.Collections.Immutable;
using Clarp.Abstractions;
using Clarp.Utils;

namespace Clarp.Concurrency;

public static class AgentGlobals {

    /// <summary>
    /// Actions Enqueued by an action 
    /// </summary>
    internal static ThreadLocal<IImmutableList<IAction>> Nested = new(() => ImmutableList<IAction>.Empty);
    
    public static void ResetNested()
    {
        Nested.Value = ImmutableList<IAction>.Empty;
    }
    
    public enum ErrorModes
    {
        Fail,
        Continue
    }
}

internal interface IAction
{
    void Enqueue();
}


/// <summary>
/// An agent can be thought of as a bit like an Actor, except that the state can be read at any time via the Value property,
/// and functions are sent to the agent instead of messages. The Value property can always be read without blocking, functions
/// sent to a specific agent are processed in the order they are received, and only one function is processed at a time. Functions
/// sent to an agent are processed in batches to reduce thread switching overhead.
/// </summary>
public class Agent<T> : ARef<T>
{
    public delegate void ErrorHandlerFn(Agent<T> agent, Exception ex);

    public delegate T UpdateFn(T prev);
    
    /// <summary>
    /// An immutable queue of actions to be processed by the agent
    /// </summary>
    private class ActionQueue
    {
        public readonly ImmutableQueue<Action> _queue;
        internal readonly Exception? _exception = null;

        public ActionQueue(ImmutableQueue<Action> queue, Exception? exception)
        {
            _queue = queue;
            _exception = exception;
        }

        public static readonly ActionQueue Empty = new(ImmutableQueue<Action>.Empty, null);
    }
    
    private T _state;
    
    private ActionQueue _actionQueue = ActionQueue.Empty;

    /// <summary>
    /// Create a new agent with a default state
    /// </summary>
    public Agent()
    {
        _state = default!;
    }
    
    /// <summary>
    /// Create a new agent with the given state
    /// </summary>
    /// <param name="state"></param>
    public Agent(T state)
    {
        _state = state;
    }

    /// <summary>
    /// Set the error handler for the agent
    /// </summary>
    private ErrorHandlerFn? ErrorHandler { get; set; }
    
    /// <summary>
    /// Set the error mode for the agent
    /// </summary>
    public AgentGlobals.ErrorModes ErrorMode { get; set; } = AgentGlobals.ErrorModes.Continue;
    
    public override T Value => _state;

    /// <summary>
    /// A specific action to be executed on an agent
    /// </summary>
    sealed class Action : IAction
    {
        private readonly Agent<T> _agent;
        private readonly UpdateFn _fn;
        private readonly IExecutor _executor;

        public Action(Agent<T> agent, Agent<T>.UpdateFn fn, IExecutor executor)
        {
            _agent = agent;
            _fn = fn;
            _executor = executor;
        }

        public void Execute()
        {
            try
            {
                _executor.Execute(static self => self.DoRun(), this);
            }
            catch (Exception e)
            {
                if (_agent.ErrorHandler != null)
                    _agent.ErrorHandler(_agent, e);
            }
        }

        private void DoRun()
        {
            try
            {
                AgentGlobals.ResetNested();

                Exception? exception = null;

                try
                {
                    var oldVal = _agent._state;
                    var newVal = _fn(oldVal);
                    _agent._state = newVal;
                    _agent.NotifyWatches(oldVal, newVal);
                }
                catch (Exception e)
                {
                    exception = e;
                }

                if (exception == null)
                    ReleasePendingSends();
                else
                {
                    AgentGlobals.ResetNested();
                    if (_agent.ErrorHandler != null)
                    {
                        try
                        {
                            _agent.ErrorHandler(_agent, exception);
                        }
                        catch (Exception)
                        {
                            if (_agent.ErrorMode == AgentGlobals.ErrorModes.Continue)
                            {
                                exception = null;
                            }
                        }
                    }
                }

                var popped = false;
                var next = _agent._actionQueue;
                while (!popped)
                {
                    var prior = _agent._actionQueue;
                    next = new ActionQueue(prior._queue.Dequeue(), exception);
                    popped = Interlocked.CompareExchange(ref _agent._actionQueue, next, prior) == prior;
                }

                if (exception == null && !next._queue.IsEmpty)
                {
                    next._queue.Peek().Execute();
                }
            }
            finally
            {
                AgentGlobals.ResetNested();
            }
        }

        public void Enqueue()
        {
            _agent.Enqueue(this);
        }
    }

    public Agent<T> Send(UpdateFn fn, IExecutor executor)
    {
        var action = new Action(this, fn, executor);
        Enqueue(action);
        return this;
    }

    public Agent<T> Send(UpdateFn fn)
    {
        return Send(fn, ThreadPoolExecutorWrapper.Instance);
    }

    private void Enqueue(Agent<T>.Action action)
    {
        var queued = false;
        ActionQueue? prior = null;
        while (!queued)
        {
            prior = _actionQueue;
            var next = new ActionQueue(prior._queue.Enqueue(action), prior._exception);
            queued = Interlocked.CompareExchange(ref _actionQueue, next, prior) == prior;
        }
        
        if (prior!._queue.IsEmpty && prior._exception == null)
            action.Execute();
        
    }

    public static int ReleasePendingSends()
    {
        var sends = AgentGlobals.Nested.Value;
        if (sends == null)
            return 0;
        
        foreach (var send in sends)
        {
            send.Enqueue();
        }
        
        AgentGlobals.ResetNested();
        return sends.Count;
    }
}