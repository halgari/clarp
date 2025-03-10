﻿using System.Collections.Immutable;
using Clarp.Collections;
using Clarp.Extensions;

namespace Clarp.Tests.Collections;

public class PersistentVector
{
    [Test]
    public async Task CanCreateVectors()
    {
        var vector = PersistentVector<int>.Empty;
        for (var i = 0; i < 100; i++)
        {
            vector = vector.Cons(i);
        }
        
        await Assert.That(vector.Count).IsEqualTo(100);
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