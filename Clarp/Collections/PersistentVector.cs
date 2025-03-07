using Clarp.Utils;

namespace Clarp.Collections;

public class PersistentVector<T>
{
    private int _count;
    private readonly int _shift;
    private readonly Node _root;
    private readonly object[] _tail;

    private PersistentVector(int count, int shift, Node root, object[] tail)
    {
        _shift = shift;
        _count = count;
        _root = root;
        _tail = tail;
    }

    private static readonly AtomicReference<Thread> NOEDIT = new(null!);
    private static readonly Node EMPTY_NODE = new(NOEDIT);

    public static readonly PersistentVector<T> Empty = new(0, 5, EMPTY_NODE, []);
    
    private int TailOff
    {
        get
        {
            if (_count < 32)
                return 0;
            return ((_count - 1) >> 5) << 5;
        }
    }
    
    public int Count => _count;
    
    public object[] ArrayFor(int i)
    {
        if (i >= 0 && i < _count)
        {
            if (i >= TailOff)
                return _tail;
            var node = _root;
            for (var level = _shift; level > 0; level -= 5)
            {
                node = (Node) node._array[(i >> level) & 0x01f];
            }
            return node._array;
        }
        throw new IndexOutOfRangeException();
    }
    
    public PersistentVector<T> Cons(T val)
    {
        if (_count - TailOff < 32)
        {
            var newTail = GC.AllocateUninitializedArray<object>(_tail.Length + 1);
            _tail.CopyTo(newTail, 0);
            newTail[_tail.Length] = val;
            return new PersistentVector<T>(_count + 1, _shift, _root, newTail);
        }
        
        Node newroot;
        var tailNode = new Node(_root._edit, _tail);
        var newshift = _shift;
        
        if ((_count >> 5) > (1 << _shift))
        {
            newroot = new Node(_root._edit);
            newroot._array[0] = _root;
            newroot._array[1] = PushTail(_shift, newroot, tailNode);
            newshift += 5;
        }
        else
        {
            newroot = PushTail(_shift, _root, tailNode);
        }
        
        return new PersistentVector<T>(_count + 1, newshift, newroot, new [] {(object)val});
    }
    
    private Node PushTail(int level, Node parent, Node tailNode)
    {
        var subidx = ((_count - 1) >> level) & 0x01f;
        var newParent = new Node(parent._edit, parent._array);
        var nodeToInsert = level == 5 ? tailNode : PushTail(level - 5, (Node) parent._array[subidx], tailNode);
        newParent._array[subidx] = nodeToInsert;
        return newParent;
    }
    
    private class Node
    {
        public AtomicReference<Thread> _edit;
        public object[] _array;
        
        public Node(AtomicReference<Thread> edit, object[] array)
        {
            _edit = edit;
            _array = array;
        }

        public Node(AtomicReference<Thread> edit)
        {
            _edit = edit;
            _array = GC.AllocateUninitializedArray<object>(32);
        }

    }

    public T this[int size]
    {
        get
        {
            var array = ArrayFor(size);
            return (T)array[size & 0x01f];
        }
    }
}