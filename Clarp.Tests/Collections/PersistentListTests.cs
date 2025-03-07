using Clarp.Abstractions;
using Clarp.Extensions;

namespace Clarp.Tests.Collections;

public class PersistentListTests
{
    [Test]
    public async Task CreateFromMutableList()
    {
        var lst = new List<int> {1, 2, 3, 4, 5}.ToPersistentList();

        await Assert.That(lst.Count).IsEqualTo(5);
    }

    [Test]
    public async Task CanDeconstructLists()
    {
        int a, b, c, d, e, f;
        ISeq<int> tail;
        
        var lst = Enumerable.Range(0, 10).ToList().ToPersistentList();

        (a, tail) = lst;
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(tail.Count).IsEqualTo(9);
        
        (a, b, tail) = lst;
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(b).IsEqualTo(1);
        await Assert.That(tail.Count).IsEqualTo(8);
        
        (a, b, c, tail) = lst;
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(b).IsEqualTo(1);
        await Assert.That(c).IsEqualTo(2);
        await Assert.That(tail.Count).IsEqualTo(7);
        
        (a, b, c, d, tail) = lst;
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(b).IsEqualTo(1);
        await Assert.That(c).IsEqualTo(2);
        await Assert.That(d).IsEqualTo(3);
        await Assert.That(tail.Count).IsEqualTo(6);
        
        (a, b, c, d, e, tail) = lst;
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(b).IsEqualTo(1);
        await Assert.That(c).IsEqualTo(2);
        await Assert.That(d).IsEqualTo(3);
        await Assert.That(e).IsEqualTo(4);
        await Assert.That(tail.Count).IsEqualTo(5);
        
        (a, b, c, d, e, f, tail) = lst;
        await Assert.That(a).IsEqualTo(0);
        await Assert.That(b).IsEqualTo(1);
        await Assert.That(c).IsEqualTo(2);
        await Assert.That(d).IsEqualTo(3);
        await Assert.That(e).IsEqualTo(4);
        await Assert.That(f).IsEqualTo(5);
        await Assert.That(tail.Count).IsEqualTo(4);
    }
}