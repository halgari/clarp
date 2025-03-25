namespace Clarp.Abstractions;

/// <summary>
/// Interface for objects tha that have metadata. Data that is not part of the object itself, but is used to describe it.
/// </summary>
public interface IMeta
{
    /// <summary>
    /// Get the attached metadata
    /// </summary>
    object Meta { get; }
}