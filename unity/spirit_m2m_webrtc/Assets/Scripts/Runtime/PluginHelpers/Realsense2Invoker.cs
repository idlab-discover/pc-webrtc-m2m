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
   

    #region Single Capturer functions
    #region General functions
    [DllImport(dllName)]
    public static extern IntPtr create_new_capturer
    (
      UInt32 fps, FrameMode frame_mode, FrameCleanupSettingsEx cleanup_settings, CaptureType capType, IntPtr captureSettings
    );
    [DllImport(dllName)]
    public static extern void start_capturing(IntPtr cap, bool startCaptureThread);
    [DllImport(dllName)]
    public static extern void set_capturer_frame_cleanup_settings(IntPtr cap, FrameCleanupSettingsEx cleanup_settings);
    [DllImport(dllName)]
    public static extern IntPtr get_calibration(IntPtr cap);
    [DllImport(dllName)]
    public static extern UInt32 get_calibration_size(IntPtr cap);
    // [Warning] Setting this to any non null function will disable polling!

    public delegate void frameReadyCallback(uint capturerID, IntPtr framePtr, bool isFrameValid);
    [DllImport(dllName, CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_frame_ready_callback(IntPtr cap, frameReadyCallback cb);
    #endregion

    #region Poll functions
    [DllImport(dllName)]
    public static extern IntPtr poll_single_frame(IntPtr cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_point_cloud(IntPtr cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_frame(IntPtr cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_raw_frame(IntPtr cap);
    #endregion

    #endregion

    #region Multi Capturer functions
    #region General functions
    [DllImport(dllName)]
    public static extern IntPtr create_new_multi_capturer
    (
     UInt32 fps, FrameMode frame_mode, FrameCleanupSettingsEx cleanup_settings, CaptureType capType, uint n_settings, IntPtr[] captureSettings
    );
    [DllImport(dllName)]
    public static extern void start_capturing_multi(IntPtr cap, bool startCaptureThread);
    [DllImport(dllName)]
    public static extern void set_cleanup_settings_for_capturer(IntPtr multi_cap, uint capturer_index, FrameCleanupSettingsEx cleanup_settings);
    [DllImport(dllName)]
    public static extern IntPtr get_calibration_for_capturer(IntPtr multi_cap, uint capturer_index);
    [DllImport(dllName)]
    public static extern uint get_calibration_size_for_capturer(IntPtr multi_cap, uint capturer_index);
    [DllImport(dllName)]
    public static extern bool register_frame_ready_callback_for_capturer(IntPtr multi_cap, uint capturer_index, IntPtr cb);

    #endregion

    #region Poll functions
    [DllImport(dllName)]
    public static extern IntPtr get_single_combined_point_cloud(IntPtr multi_cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_combined_point_cloud(IntPtr multi_cap);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_frame_for_capturer(IntPtr multi_cap, uint capturer_index);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_point_cloud_for_capturer(IntPtr multi_cap, uint capturer_index);
    [DllImport(dllName)]
    public static extern IntPtr poll_next_raw_frame_for_capturer(IntPtr multi_cap, uint capturer_index);
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
    [DllImport(dllName)]
    public static extern IntPtr convert_frame_to_pc(IntPtr frame);
    [DllImport(dllName)]
    public static extern IntPtr convert_frame_to_raw_frame(IntPtr frame);
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
    public static extern void free_multi_capturer(IntPtr cap);
    [DllImport(dllName)]
    public static extern void free_capturer_calibration(CaptureType type, IntPtr cal);
    #endregion

}
