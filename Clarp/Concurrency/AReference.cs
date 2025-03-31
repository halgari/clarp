using System.Collections.Immutable;
using Clarp.Abstractions;

namespace Clarp.Concurrency;

/// <summary>
///     Abstract base class for a reference that holds metadata.
/// </summary>
public abstract class AReference : IReference
{
    private readonly IImmutableDictionary<object, object> _meta;

    /// <summary>
    ///     Initializes a new instance of the <see cref="AReference" /> class with empty metadata.
    /// </summary>
    protected AReference()
    {
        _meta = ImmutableDictionary<object, object>.Empty;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="AReference" /> class with specified metadata.
    /// </summary>
    /// <param name="meta">An immutable dictionary containing metadata.</param>
    protected AReference(IImmutableDictionary<object, object> meta)
    {
        _meta = meta;
    }

    /// <summary>
    ///     Gets the metadata associated with the reference.
    /// </summary>
    public object Meta => _meta;

    /// <summary>
    ///     Alters the metadata using the provided function.
    /// </summary>
    /// <param name="alterer">A function that alters the metadata.</param>
    /// <returns>The altered metadata.</returns>
    public object AlterMeta(Func<object> alterer)
    {
        throw new NotImplementedException();
    }

    /// <summary>
    ///     Resets the metadata to the specified value.
    /// </summary>
    /// <param name="meta">The new metadata value.</param>
    /// <returns>The previous metadata value.</returns>
    public object ResetMeta(object meta)
    {
        throw new NotImplementedException();
    }
}