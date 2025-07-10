using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public static class DLLLogger 
{
    public delegate void debugCallback(IntPtr request, int color, int size);
    public delegate void logToFileCallback(IntPtr request, int size, bool writeToWebsocket);

    enum Color { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(debugCallback))]
    public static void OnDebugCallback(IntPtr request, int color, int size)
    {
        // Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);
        // Add specified color
        debug_string =
            String.Format("Realsense Capturing: {0}{1}{2}{3}{4}",
            "<color=",
            ((Color)color).ToString(), ">", debug_string, "</color>");
        // Log the string
        Debug.Log(debug_string);
    }

    [MonoPInvokeCallback(typeof(logToFileCallback))]
    public static void OnLogToFileCallback(IntPtr request, int size, bool writeToWebsocket)
    {
        string logger_string = Marshal.PtrToStringAnsi(request, size);
        Logger.Log(logger_string, writeToWebsocket);
    }
}
