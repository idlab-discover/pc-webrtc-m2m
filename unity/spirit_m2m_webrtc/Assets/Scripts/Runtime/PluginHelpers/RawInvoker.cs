using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

public static class RawInvoker
{
    private const string dllName = "spirit_idlab_raw";
   
    [DllImport(dllName)]
    public static extern void set_logging(string log_directory, int logLevel);

    
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterDebugCallback(DLLLogger.debugCallback cb);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterLogToFileCallback(DLLLogger.logToFileCallback cb);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr create_encoding_queue(uint width, uint height, 
        uint max_queue, uint n_workers, uint n_capturers,
        ColorCodecType col_codec, IntPtr col_codec_settings,
        DepthCodecType dep_codec, IntPtr dep_codec_settings
        );
    [DllImport(dllName)]
    public static extern void clean_up();

    [DllImport(dllName)]
    public static extern int encode_frame(IntPtr enc_queue, IntPtr f);


    public delegate void colorDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 capturerID, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp);
    public delegate void depthDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 capturerID, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp);
    public delegate void freeFrameCallback(IntPtr cb);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_color_done_callback(IntPtr enc_queue, IntPtr cbPtr);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_depth_done_callback(IntPtr enc_queue, IntPtr cbPtr);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_free_frame_callback(IntPtr enc_queue, IntPtr cbPtr);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr decode_depth(IntPtr depth_dec, IntPtr data, uint width, uint height);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr decode_color(IntPtr color_dec, IntPtr data, ulong size, uint width, uint height);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr get_decoded_depth_data(IntPtr data);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr get_decoded_color_data(IntPtr data);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_encoding_queue(IntPtr enc_queue);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_decoded_depth(IntPtr data);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_decoded_color(IntPtr data);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_color_decoder(IntPtr data);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_depth_decoder(IntPtr data);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr create_color_decoder(ColorCodecType codec);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr create_depth_decoder(DepthCodecType codec);


}
