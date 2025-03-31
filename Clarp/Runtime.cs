using System.Runtime.CompilerServices;
using Clarp.Concurrency;
using Clarp.Utils;

namespace Clarp;

/// <summary>
/// A helper class for wrapping several common runtime functions.
/// </summary>
public static class Runtime
{
    /// <summary>
    /// Runs the given function in a transaction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static T DoSync<T>(in Func<T> f) 
        => LockingTransaction.RunInTransaction(static f => f(), f);

    /// <summary>
    /// Runs the given function in a transaction.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void DoSync(in Action f)
    {
        LockingTransaction.RunInTransaction(static f =>
        {
            f();
            return 0;
        }, f);
    }
    
    /// <summary>
    /// Runs the given function in a transaction, passing the given state to it.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static void DoSync<TState>(in Action<TState> f, in TState state)
        where TState : allows ref struct
    {
        LockingTransaction.RunInTransaction(static input =>
        {
            var (f, state) = input;
            f(state);
            return 0;
        }, RefTuple.Create(f, state));
    }

    /// <summary>
    /// Runs the given function in a transaction, passing the given state to it and returning the result.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static TResult DoSync<TState, TResult>(Func<TState, TResult> f, TState state) 
        where TState : allows ref struct 
    {
        return LockingTransaction.RunInTransaction(static input =>
        {
            var (f, state) = input;
            return f(state);
        }, RefTuple.Create(f, state));
    }
}