using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

public static class UnsafeUtils
{
    public static unsafe ref T RefAt<T>(this NativeArray<T> _array, int _index) where T : unmanaged
    {
        return ref UnsafeUtility.ArrayElementAsRef<T>(_array.GetUnsafePtr(), _index);
    }

    public static unsafe void Sort<T>(this NativeArray<T> _array, Comparer<T> _comp) where T : unmanaged
    {
        var len = _array.Length;
        var ptr = (T*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(_array);
        NativeSortExtension.Sort(ptr, len, _comp);
    }
}