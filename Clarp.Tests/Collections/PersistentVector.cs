using System.Collections.Immutable;
using Clarp.Collections;
using Clarp.Extensions;

namespace Clarp.Tests.Collections;

public class PersistentVector
{
    [Test]
    public async Task CanCreateVectors()
    {
        var vector = PersistentVector<int>.Empty;
        for (int i = 0; i < 100; i++)
        {
            vector = vector.Cons(i);
        }

        var plist = Enumerable.Range(0, 100).ToImmutableList();

        Assert.Fail("ble");
    }

    [Test]
    public async Task CanIndexVectors()
    {
        var vector = Enumerable.Range(0, 100).ToPersistentVector();
        for (int i = 0; i < 100; i++)
        {
            await Assert.That(vector[i]).IsEqualTo(i);
        }
    }
}