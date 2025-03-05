using System.Collections;
using Clarp.Collections;

namespace Clarp.Abstractions;

/// <summary>
/// A Seq is a sequence of elements, somewhat like a single-linked-list, but may be lazily generated. It is
/// immutable, and is the foundation of many abstractions in Clarp.
/// </summary>
public interface ISeq<T> : IEnumerable<T>
{
    /// <summary>
    /// Get the value at this position in the sequence.
    /// </summary>
    public T First { get; }

    /// <summary>
    /// Move to the next element in the sequence.
    /// </summary>
    public ISeq<T> Next();

    /// <summary>
    /// Returns true if the sequence is empty, in order for this to work, all seqs must return EmptyList.Instance
    /// when they are empty.
    /// </summary>
    public bool IsEmpty => ReferenceEquals(this, EmptyList<T>.Instance);
    
    /// <summary>
    /// Struct enumerator for the sequence.
    /// </summary>
    public new SeqEnumerator<T> GetEnumerator()
    {
        return new SeqEnumerator<T>(this);
    }

    IEnumerator<T> IEnumerable<T>.GetEnumerator()
    {
        return GetEnumerator();
    }
    
    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}


public struct SeqEnumerator<T> : IEnumerator<T>
{
    private bool _started = false;
    private ISeq<T> _seq;

    public SeqEnumerator(ISeq<T> seq)
    {
        _seq = seq;
    }

    public bool MoveNext()
    {
        if (!_started)
        {
            _started = true;
            return !_seq.IsEmpty;
        }
        _seq = _seq.Next();
        return !_seq.IsEmpty;
    }

    public void Reset() => throw new NotSupportedException();
    
    public T Current => _seq.First;

    T IEnumerator<T>.Current => _seq.First;

    object? IEnumerator.Current => _seq.First;

    public void Dispose()
    {
        
    }
}