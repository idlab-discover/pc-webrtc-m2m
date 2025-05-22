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
    public static extern void clean_up();


    [DllImport(dllName)]
    public static extern CapturerIntrinsics get_depth_intrinsics(IntPtr cap);
    [DllImport(dllName)]
    public static extern CapturerIntrinsics get_color_intrinsics(IntPtr cap);
   

    #region Capturer functions
    #region General functions
    [DllImport(dllName)]
    public static extern IntPtr create_new_capturer
    (
      UInt32 fps, FrameMode frame_mode, FrameCleanupSettingsEx cleanup_settings, CaptureType capType, IntPtr captureSettings
    );
    [DllImport(dllName)]
    public static extern void start_capturing(IntPtr cap);
    [DllImport(dllName)]
    public static extern void set_capturer_frame_cleanup_settings(IntPtr cap, FrameCleanupSettingsEx cleanup_settings);
    [DllImport(dllName)]
    public static extern IntPtr get_calibration(IntPtr cap);
    #endregion

    #region Poll functions
    [DllImport(dllName)]
    public static extern IntPtr poll_next_point_cloud(IntPtr cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_frame(IntPtr cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_raw_frame(IntPtr cap);
    #endregion

    #endregion

    #region Point cloud functions
    [DllImport(dllName)]
    public static extern uint get_point_cloud_size(IntPtr pc);
    #endregion

    #region Frame functions
    [DllImport(dllName)]
    public static extern uint get_frame_size(IntPtr frame);
    [DllImport(dllName)]
    public static extern IntPtr get_raw_depth(IntPtr frame);
    [DllImport(dllName)]
    public static extern IntPtr get_raw_color(IntPtr frame);
    #endregion

    #region Raw converter functions
    [DllImport(dllName)]
    public static extern IntPtr create_new_raw_converter(CaptureType type, IntPtr cal);

    [DllImport(dllName)]
    public static extern void convert_raw_frame(IntPtr c, IntPtr depth, IntPtr color, IntPtr pos_out, IntPtr col_out);
    #endregion

    #region Free memory functions
    [DllImport(dllName)]
    public static extern void free_point_cloud(IntPtr pc);
    [DllImport(dllName)]
    public static extern void free_frame(IntPtr frame);
    [DllImport(dllName)]
    public static extern void free_raw_frame(IntPtr frame);
    [DllImport(dllName)]
    public static extern void free_raw_converter(IntPtr c);
    [DllImport(dllName)]
    public static extern void free_capturer(IntPtr cap);
    [DllImport(dllName)]
    public static extern void free_capturer_calibration(CaptureType type, IntPtr cal);
    #endregion

}
