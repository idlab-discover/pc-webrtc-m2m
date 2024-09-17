using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

public static class DracoInvoker
{
    private const string dllName = "spirit_idlab_draco";

    [DllImport(dllName)]
    public static extern void set_logging(string log_directory, int logLevel);

    
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterDebugCallback(DLLLogger.debugCallback cb);
    [DllImport(dllName)]
    public static extern int initialize();
    [DllImport(dllName)]
    public static extern void clean_up();

    [DllImport(dllName)]
    public static extern int encode_pc(IntPtr pc);
    [DllImport(dllName)]
    public static extern UInt32 get_encoded_size(IntPtr enc);
    [DllImport(dllName)]
    public static extern IntPtr get_raw_data(IntPtr enc);

    [DllImport(dllName)]
    unsafe public static extern IntPtr decode_pc(byte* data, UInt32 size);

    [DllImport(dllName)]
    public static extern UInt32 get_n_points(IntPtr dec);
    [DllImport(dllName)]
    public static extern IntPtr get_point_array(IntPtr dec);
    [DllImport(dllName)]
    public static extern IntPtr get_color_array(IntPtr dec);

    [DllImport(dllName)]
    public static extern void free_encoder(IntPtr enc);
    [DllImport(dllName)]
    public static extern void free_decoder(IntPtr enc);
    [DllImport(dllName)]
    public static extern void free_description(IntPtr dsc);


    public delegate void descriptionDoneCallback(IntPtr dsc, IntPtr rawDataPtr, UInt32 totalPointsInCloud, UInt32 dscSize, UInt32 frameNr, UInt32 dscNr);
    public delegate void freePCCallback(IntPtr cb);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_description_done_callback(descriptionDoneCallback cb);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_free_pc_callback(freePCCallback cb);

}
