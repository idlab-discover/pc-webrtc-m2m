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
    public static extern int initialize(UInt32 width, UInt32 height, UInt32 fps, float min_dist, float max_dist, bool use_cam);
    [DllImport(dllName)]
    public static extern void clean_up();

    [DllImport(dllName)]
    public static extern IntPtr poll_next_point_cloud();
    [DllImport(dllName)]
    public static extern uint get_point_cloud_size(IntPtr pc);
    [DllImport(dllName)]
    public static extern void free_point_cloud(IntPtr pc);
}
