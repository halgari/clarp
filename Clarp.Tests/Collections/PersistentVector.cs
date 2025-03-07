using System.Collections.Immutable;
using Clarp.Collections;
using Clarp.Extensions;

namespace Clarp.Tests.Collections;

public class PersistentVector
{
    private static readonly int MAX_SIZE = (int)Math.Pow(PersistentVector<int>.BRANCH_FACTOR, 3);
    
    [Test]
    public async Task CanCreateVectors()
    {
        var vector = PersistentVector<int>.Empty;
        for (var i = 0; i < MAX_SIZE; i++)
        {
            vector = vector.Cons(i);
        }
        
        await Assert.That(vector.Count).IsEqualTo(MAX_SIZE);
    }

    [Test]
    public async Task CanIndexVectors()
    {
        var vector = Enumerable.Range(0, MAX_SIZE).ToPersistentVector();
        for (int i = 0; i < MAX_SIZE; i++)
        {
            await Assert.That(vector[i]).IsEqualTo(i);
        }
    }
}