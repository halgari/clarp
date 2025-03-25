namespace Clarp.Abstractions;

/// <summary>
/// Interface for a reference object, such as an atom, ref or agent.
/// </summary>
public interface IReference : IMeta
{
    /// <summary>
    /// Atomically update the reference's metadata. This method may be called multiple times if there
    /// is contention on the reference.
    /// </summary>
    object AlterMeta(Func<object> alterer);

    /// <summary>
    /// Reset the reference's metadata to a new value without regard to the current value.
    /// </summary>
    object ResetMeta(object meta);
}