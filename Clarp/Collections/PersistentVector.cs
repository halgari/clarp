using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Clarp.Utils;

namespace Clarp.Collections;

public sealed class PersistentVector<T>
{
    /// <summary>
    /// The bit shift to multiply or divide by BranchFactor
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private const int SHIFT = 5;

    /// <summary>
    /// The branching factor of the tree, 32 in almost all cases (Clojure for example)
    /// </summary>
    // ReSharper disable once InconsistentNaming
    public const int BRANCH_FACTOR = 1 << SHIFT;
    
    /// <summary>
    /// The mask for the first SHIFT bits
    /// </summary>
    // ReSharper disable once InconsistentNaming
    private const int MASK = BRANCH_FACTOR - 1;
    
    
    private readonly int _count;
    private readonly int _shift;
    private readonly INode _root;
    private InlinedValues _tail;

    private PersistentVector(int count, int shift, INode root)
    {
        _shift = shift;
        _count = count;
        _root = root;
    }

    /// <summary>
    /// Create a new persistent vector with the only tail value being the given value
    /// </summary>
    private PersistentVector(int count, int newshift, Node newroot, T onlyTailValue)
    {
        _count = count;
        _shift = newshift;
        _root = newroot;
        _tail[0] = onlyTailValue;
    }

    /// <summary>
    /// Create a new persistent vector with the given value added to the tail
    /// </summary>
    private PersistentVector(PersistentVector<T> other, int addedPosition, T tailAddition)
    {
        _count = other._count + 1;
        _shift = other._shift;
        _root = other._root;
        _tail = other._tail;
        _tail[addedPosition] = tailAddition;
    }

    private PersistentVector(int count, int shift, INode root, in InlinedValues tail, int tailPos, T tailValue)
    {
        _count = count;
        _shift = shift;
        _root = root;
        _tail = tail;
        _tail[tailPos] = tailValue;
    }

    private PersistentVector(PersistentVector<T> baseVector, INode newRoot)
    {
        _count = baseVector._count;
        _shift = baseVector._shift;
        _root = newRoot;
        _tail = baseVector._tail;
    }

    private static readonly AtomicReference<Thread> NOEDIT = new(null!);
    private static readonly Node EMPTY_NODE = new(NOEDIT);

    public static readonly PersistentVector<T> Empty = new(0, SHIFT, EMPTY_NODE);
    
    private int TailOff
    {
        get
        {
            if (_count < 32)
                return 0;
            return ((_count - 1) >> SHIFT) << SHIFT;
        }
    }
    
    public int Count => _count;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)] 
    private ReadOnlySpan<T> GetReadOnlySpanFor(int i)
    {
        if (i >= 0 && i < _count)
        {
            if (i >= TailOff)
                return _tail;
            var node = _root;
            for (var level = _shift; level > 0; level -= SHIFT)
            {
                node = ((Node)node)[(i >> level) & MASK];
            }

            return ((ValueNode)node).GetReadOnlySpan();
        }
        throw new IndexOutOfRangeException();
    }
    
    public PersistentVector<T> Cons(T val)
    {
        int tailOff = TailOff;
        var remainder = _count - tailOff;
        if (remainder < 32)
        {
            return new PersistentVector<T>(this, remainder, val);
        }
        
        Node newroot;
        var tailNode = new ValueNode(_root.Edit, GetReadOnlySpan());
        var newshift = _shift;
        
        if ((_count >> 5) > (1 << _shift))
        {
            newroot = new Node(_root.Edit, _root, NewPath(_root.Edit, _shift, tailNode));
            newshift += 5;
        }
        else
        {
            newroot = PushTail(_shift, (Node)_root, tailNode);
        }
        
        return new PersistentVector<T>(_count + 1, newshift, newroot, val);
    }

    public PersistentVector<T> AssocN(int i, T val)
    {
        if (i >= 0 && i < _count)
        {
            if (i >= TailOff)
            {
                var newTail = _tail;
                newTail[i & MASK] = val;
                return new PersistentVector<T>(_count, _shift, _root, newTail, i & MASK, val);
            }

            return new PersistentVector<T>(this, DoAssoc(_shift, _root, i, val));
        }
        if (i == _count)
            return Cons(val);
        throw new IndexOutOfRangeException();
    }

    private static INode DoAssoc(int level, INode node, int i, T val)
    {
        if (level == 0)
        {
            return new ValueNode((ValueNode)node, i & MASK, val);
        }
        else
        {
            var subidx = (i >> level) & MASK;
            var newSubNode = DoAssoc(level - SHIFT, ((Node)node)[subidx], i, val);
            return new Node((Node)node, subidx, newSubNode);
        }
    }

    private static INode NewPath(AtomicReference<Thread> edit, int level, INode node)
    {
        if (level == 0)
            return node;
        return new Node(edit, NewPath(edit, level - SHIFT, node));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ReadOnlySpan<T> GetReadOnlySpan() 
        => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(ref _tail[0]), BRANCH_FACTOR);
    

    private Node PushTail(int level, Node parent, ValueNode tailNode)
    {
        var subIdx = ((_count - 1) >> level) & MASK;
        INode nodeToInsert;
        if(level == 5)
        {
            nodeToInsert = tailNode;
        }
        else
        {
            Node child = (Node) parent[subIdx];
            nodeToInsert = (child != null)?
                PushTail(level-5,child, tailNode)
                :NewPath(_root.Edit,level-5, tailNode);
        }
        
        Node ret = new Node(parent, subIdx, nodeToInsert );
        return ret;
            
    }
    
    public T this[int size]
    {
        get
        {
            var array = GetReadOnlySpanFor(size);
            return array[size & MASK];
        }
    }
    
    public interface INode
    {
        public AtomicReference<Thread> Edit { get; }
    }

    [InlineArray(BRANCH_FACTOR)]
    private struct InlinedValues
    {
        private T _value;
    }
    
    private class ValueNode : INode
    {
        public AtomicReference<Thread> Edit { get; }
        public InlinedValues _values;

        public ValueNode(AtomicReference<Thread> edit, ReadOnlySpan<T> values)
        {
            Edit = edit;
            values.CopyTo(GetSpan());
        }

        public ValueNode(ValueNode node, int values, T val)
        {
            Edit = node.Edit;
            var selfSpan = GetSpan();
            node.GetReadOnlySpan().CopyTo(selfSpan);
            selfSpan[values] = val;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        internal ReadOnlySpan<T> GetReadOnlySpan() 
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(ref _values[0]), BRANCH_FACTOR);
        
        private Span<T> GetSpan() 
            => MemoryMarshal.CreateSpan(ref _values[0], BRANCH_FACTOR);

    }
    



    [InlineArray(BRANCH_FACTOR)]
    private struct InlineNodes
    {
        INode _node;
    }
    
    private class Node : INode
    {
        public AtomicReference<Thread> Edit { get; }
        private InlineNodes _array;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private ReadOnlySpan<INode> GetReadOnlySpan() 
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(ref _array[0]), BRANCH_FACTOR);
        
        private Span<INode> GetSpan() 
            => MemoryMarshal.CreateSpan(ref _array[0], BRANCH_FACTOR);

        public Node(AtomicReference<Thread> edit)
        {
            Edit = edit;
        }

        internal Node(Node other, int subidx, INode nodeToInsert)
        {
            Edit = other.Edit;
            var selfSpan = GetSpan();
            other.GetReadOnlySpan().CopyTo(selfSpan);
            selfSpan[subidx] = nodeToInsert;
        }

        public Node(AtomicReference<Thread> edit, INode slot0)
        {
            Edit = edit;
            _array[0] = slot0;
        }
        
        public Node(AtomicReference<Thread> edit, INode slot0, INode slot1)
        {
            Edit = edit;
            _array[0] = slot0;
            _array[1] = slot1;
        }
        
        public INode this[int idx]
        {
            get
            {
                Debug.Assert(idx is >= 0 and < BRANCH_FACTOR, "Index out of bounds");
                return _array[idx];
            }
        }
    }


}