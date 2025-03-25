using System.Collections.Immutable;
using Clarp.Abstractions;

namespace Clarp.Concurrency;

public class AReference : IReference
{
    private IImmutableDictionary<object, object> _meta;

    public AReference()
    {
        _meta = ImmutableDictionary<object, object>.Empty;
    }
    
    public AReference(IImmutableDictionary<object, object> meta)
    {
        _meta = meta;
    }


    public object Meta => _meta;
    
    public object AlterMeta(Func<object> alterer)
    {
        throw new NotImplementedException();
    }

    public object ResetMeta(object meta)
    {
        throw new NotImplementedException();
    }
}