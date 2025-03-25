using System.Runtime.CompilerServices;

namespace Clarp.Utils;

public static class Atomic
{
    public static unsafe bool CompareAndSet<T>(ref T location, T comparand, T newValue)
    {
        if (typeof(T).IsValueType)
        {
            return sizeof(T) switch
            {
                1 => CompareAndSet8(ref Unsafe.As<T, byte>(ref location), Unsafe.As<T, byte>(ref comparand),
                    Unsafe.As<T, byte>(ref newValue)),
                2 => CompareAndSet16(ref Unsafe.As<T, short>(ref location), Unsafe.As<T, short>(ref comparand),
                    Unsafe.As<T, short>(ref newValue)),
                4 => CompareAndSet32(ref Unsafe.As<T, int>(ref location), Unsafe.As<T, int>(ref comparand),
                    Unsafe.As<T, int>(ref newValue)),
                8 => CompareAndSet64(ref Unsafe.As<T, long>(ref location), Unsafe.As<T, long>(ref comparand),
                    Unsafe.As<T, long>(ref newValue)),
                _ => throw new NotSupportedException("Unsupported value type size of: " + sizeof(T))
            };
        }
        return CompareAndSetRef(ref Unsafe.As<T, object>(ref location), Unsafe.As<T, object>(ref comparand),
            Unsafe.As<T, object>(ref newValue));
    }
    
    public static bool CompareAndSetRef(ref object location, object comparand, object newValue)
        => ReferenceEquals(Interlocked.CompareExchange(ref location, newValue, comparand), comparand);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool CompareAndSet8(ref byte location, byte comparand, byte newValue) 
        => Interlocked.CompareExchange(ref location, newValue, comparand) == comparand;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool CompareAndSet16(ref short location, short comparand, short newValue) 
        => Interlocked.CompareExchange(ref location, newValue, comparand) == comparand;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool CompareAndSet32(ref int location, int comparand, int newValue) 
        => Interlocked.CompareExchange(ref location, newValue, comparand) == comparand;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    public static bool CompareAndSet64(ref long location, long comparand, long newValue) 
        => Interlocked.CompareExchange(ref location, newValue, comparand) == comparand;
}