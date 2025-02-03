using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;


public static class DLLWrapper
{
    public static void LoggingInit(int loggingLevel)
    {
        Realsense2Invoker.RegisterDebugCallback(DLLLogger.OnDebugCallback);
        Realsense2Invoker.set_logging("", loggingLevel);
        DracoInvoker.RegisterDebugCallback(DLLLogger.OnDebugCallback);
        DracoInvoker.set_logging("", loggingLevel);
        WebRTCInvoker.RegisterDebugCallback(DLLLogger.OnDebugCallback);
        WebRTCInvoker.set_logging("", loggingLevel);
    }
}
