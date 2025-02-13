using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

[StructLayout(LayoutKind.Explicit)]
public unsafe struct CapturerIntrinsics
{

    [FieldOffset(0)]
    public uint width;

    [FieldOffset(4)]
    public uint height;

    [FieldOffset(8)]
    public uint model;

    [FieldOffset(12)]
    public float ppx;

    [FieldOffset(16)]
    public float ppy;

    [FieldOffset(20)]
    public float fx;

    [FieldOffset(24)]
    public float fy;

    [FieldOffset(28)]
    public fixed float coeff[5];

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string ToString()
    {
        return $"W: {width} | H: {height} | M: {model} | PPX: {ppx} | PPY: {ppy} | FX: {fx} | FY: {fy} | COEF [{coeff[0]}, {coeff[1]}, {coeff[2]}, {coeff[3]}, {coeff[4]}]";
    }
}
