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
    private const int BRANCH_FACTOR = 1 << SHIFT;
    
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
                node = ((Node)node)._array[(i >> level) & MASK];
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
            newroot = new Node(_root.Edit);
            newroot._array[0] = _root;
            newroot._array[1] = PushTail(_shift, newroot, tailNode);
            newshift += 5;
        }
        else
        {
            newroot = PushTail(_shift, (Node)_root, tailNode);
        }
        
        return new PersistentVector<T>(_count + 1, newshift, newroot, val);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    internal ReadOnlySpan<T> GetReadOnlySpan() 
        => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(ref _tail[0]), BRANCH_FACTOR);
    

    private Node PushTail(int level, Node parent, INode tailNode)
    {
        var subidx = ((_count - 1) >> level) & MASK;
        var nodeToInsert = level == SHIFT ? tailNode : PushTail(level - SHIFT, (Node) parent._array[subidx], tailNode);
        return parent.WithNode(subidx, nodeToInsert);

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
        public InlineNodes _array;
        
        [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
        private ReadOnlySpan<INode> GetReadOnlySpan() 
            => MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(ref _array[0]), BRANCH_FACTOR);
        
        private Span<INode> GetSpan() 
            => MemoryMarshal.CreateSpan(ref _array[0], BRANCH_FACTOR);

        public Node(AtomicReference<Thread> edit)
        {
            Edit = edit;
        }

        private Node(Node other, int subidx, INode nodeToInsert)
        {
            Edit = other.Edit;
            var selfSpan = GetSpan();
            other.GetReadOnlySpan().CopyTo(selfSpan);
            selfSpan[subidx] = nodeToInsert;
        }

        /// <summary>
        /// Returns a new node with the given node inserted at the given subindex
        /// </summary>
        public Node WithNode(int subidx, INode nodeToInsert) => 
            new(this, subidx, nodeToInsert);
    }


}