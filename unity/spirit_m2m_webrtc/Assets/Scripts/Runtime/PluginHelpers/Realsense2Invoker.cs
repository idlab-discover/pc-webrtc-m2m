using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.UIElements;

public static class Realsense2Invoker
{
    private const string dllName = "spirit_idlab_realsense";

    [DllImport(dllName)]
    public static extern void set_logging(string log_directory, int logLevel);

    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterDebugCallback(DLLLogger.debugCallback cb);

    [DllImport(dllName)]
    public static extern int initialize
    (
        UInt32 width, UInt32 height, UInt32 artificial_size,
        UInt32 fps, float min_dist, float max_dist, bool use_cam, 
        FrameMode frame_mode, FrameCleanupSettingsEx cleanup_settings
    );
    [DllImport(dllName)]
    public static extern void clean_up();

    [DllImport(dllName)]
    public static extern IntPtr poll_next_point_cloud();
    [DllImport(dllName)]
    public static extern IntPtr poll_next_frame();
    [DllImport(dllName)]
    public static extern IntPtr poll_next_raw_frame();

    [DllImport(dllName)]
    public static extern uint get_point_cloud_size(IntPtr pc);
    [DllImport(dllName)]
    public static extern uint get_frame_size(IntPtr frame);
    [DllImport(dllName)]
    public static extern uint get_frame_width(IntPtr frame);
    [DllImport(dllName)]
    public static extern uint get_frame_height(IntPtr frame);
    [DllImport(dllName)]
    public static extern IntPtr get_raw_depth(IntPtr frame);
    [DllImport(dllName)]
    public static extern IntPtr get_raw_color(IntPtr frame);
    [DllImport(dllName)]
    public static extern void free_point_cloud(IntPtr pc);
    [DllImport(dllName)]
    public static extern void free_frame(IntPtr frame);
    [DllImport(dllName)]
    public static extern void free_raw_frame(IntPtr frame);
    [DllImport(dllName)]
    public static extern IntPtr create_new_raw_converter(bool use_cam, CapturerIntrinsics depth_intrinsics, CapturerIntrinsics color_intrinsics);
    
    [DllImport(dllName)]
    public static extern void convert_raw_frame(IntPtr c, IntPtr depth, IntPtr color, IntPtr pos_out, IntPtr col_out);
    [DllImport(dllName)]
    public static extern void free_raw_converter(IntPtr c);
    [DllImport(dllName)]
    public static extern CapturerIntrinsics get_depth_intrinsics();
    [DllImport(dllName)]
    public static extern CapturerIntrinsics get_color_intrinsics();

}
