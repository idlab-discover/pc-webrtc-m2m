using System;
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

    public static uint Size()
    {
        return 48;
    }

    public byte[] ConvertToBuffer()
    {
        byte[] b = new byte[Size()];
        var widthField = BitConverter.GetBytes(width);
        widthField.CopyTo(b, 0);
        var heightField = BitConverter.GetBytes(height);
        heightField.CopyTo(b, 4);
        var modelField = BitConverter.GetBytes(model);
        modelField.CopyTo(b, 8);
        var ppxField = BitConverter.GetBytes(ppx);
        ppxField.CopyTo(b, 12);
        var ppyField = BitConverter.GetBytes(ppy);
        ppyField.CopyTo(b, 16);
        var fxField = BitConverter.GetBytes(fx);
        fxField.CopyTo(b, 20);
        var fyField = BitConverter.GetBytes(fy);
        fyField.CopyTo(b, 24);
        fixed (float* ptr = coeff){
            for (int i = 0; i < 5; i++)
            {
                var coeffField = BitConverter.GetBytes(ptr[i]);
                coeffField.CopyTo(b, 28+i*4);
            }
        }
        return b;
    }
}
