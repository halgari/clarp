namespace Clarp.Abstractions;

/// <summary>
/// A container for a value that can be retrieved in a non-blocking way. By convention, the value
/// should be immutable and the container should be thread-safe.
/// </summary>
public interface IDeref<out T>
{
    /// <summary>
    /// Get the current value. Should never block.
    /// </summary>
    T Value { get; }
}