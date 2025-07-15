using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using Unity.VisualScripting;
using UnityEngine;

public class Logger 
{
    public enum Status
    {
        // Object Status
        Creating = 0,
        Created = 1,
        Destroying = 2,
        Destroyed = 3,
        Disposing = 4,
        Disposed = 5,
        Failed = 6,

        // Encoding Status
        StartEncodingRaw = 100,
        StartEncodingColor = 101,
        EndEncodingColor = 102,
        StartEncodingDepth = 103,
        EndEncodingDepth = 104,
        StartEncodingPC = 105,
        EndEncodingPC = 106,
        StartEncodingAudio = 107,
        EndEncodingAudio = 108,

        // Decoding Status
        StartDecodingRaw = 200,
        StartDecodingColor = 201,
        EndDecodingColor = 202,
        StartDecodingDepth = 203,
        EndDecodingDepth = 204,
        StartDecodingPC = 205,
        EndDecodingPC = 206,
        StartDecodingAudio = 207,
        EndDecodingAudio = 208,

        // Encoding Queue Status
        Enqueuing = 300,
        Enqueued = 301,
        Dequeuing = 302,
        Dequeued = 303,
        Dropped = 304,
        DroppedBecausePrevious = 305,

        // General Frame Status
        FrameCreated = 400,
        FrameCompleted = 401,
        FrameRendered = 402,
        FrameDropped = 403,
        FrameDestroyed = 404,

        // RawConverter Status
        StartRawConversion = 500,
        EndRawConversion = 501,

        // Debugging Status
        StartRawColorCopy = 9000,
        EndRawColorCopy = 9001,
    }
    public static long Time => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    private static readonly object lockObj = new object();
    private static StreamWriter writer;
    private static LoggerSettings loggerSettings;
    private static DateTime nextFlush;
    private static bool isInited;
    public static void Init(LoggerSettings _loggerSettings)
    {

        if(_loggerSettings.logPath == null || _loggerSettings.logPath == "")
        {
            isInited = false;
            return;
        }
        loggerSettings = _loggerSettings;
        writer = new StreamWriter(loggerSettings.logPath, false);
        writer.AutoFlush = false;
        nextFlush = DateTime.Now;
        isInited = true;

        RawInvoker.set_logging_settings(loggerSettings.pointCloud.limitLogging, loggerSettings.pointCloud.everyNFrames);


    }
    public static void Log(string message, bool writeToSocket=false)
    {
        if(!isInited)
        {
            return;
        }

        lock(lockObj)
        {
            // TODO write to socket as well if needed
            writer.WriteLine(message);
            if(loggerSettings.flushInterval == 0 || DateTime.Now > nextFlush)
            {
                writer.Flush();
                nextFlush = DateTime.Now.AddMilliseconds(loggerSettings.flushInterval);
            }
        }
    }
    
   
    public static void ForceFlush()
    {
        if(isInited)
        {
            writer.FlushAsync();
            nextFlush = DateTime.Now.AddMilliseconds(loggerSettings.flushInterval);
        }
    }

    #region Conditional Logging
    private static string LogCommon(string name, Logger.Status status) => $"id={name} ts={Time} status={((int)status)}";
    private static string LogCommonClientEnc(string name, Logger.Status status, uint clientID, uint capturerID, uint frameNr) => $"{LogCommonClient(name, status, clientID)} capturerID={capturerID} frameNr={frameNr}";
    private static string LogCommonEnc(string name, Logger.Status status, uint capturerID, uint frameNr) => $"{LogCommon(name, status)} capturerID={capturerID} frameNr={frameNr}";
    private static string LogCommonClient(string name, Logger.Status status, uint clientID) => $"{LogCommon(name, status)} clientID={clientID}";

    [Conditional("ENABLE_LOGGING")]
    public static void LogStatus(string name, Logger.Status status)
    {
        Log(LogCommon(name, status));
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogStatusClient(string name, Logger.Status status, uint clientID)
    {
        Log(LogCommonClient(name, status, clientID));
    }
    [Conditional("ENABLE_LOGGING")]
    public static void LogStatusClientAndCapturer(string name, Logger.Status status, uint clientID, uint capturerID)
    {
        Log($"{LogCommonClient(name, status, clientID)} capturerID={capturerID}");
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogPCFrameStatus(string name, Logger.Status status, uint capturerID, uint frameNr)
    {
        
        Log(LogCommonEnc(name, status, capturerID, frameNr));
    }
    [Conditional("ENABLE_LOGGING")]
    public static void LogPCFrameStatus(string name, Logger.Status status, uint clientID, uint capturerID, uint frameNr)
    {

        Log(LogCommonClientEnc(name, status, clientID, capturerID, frameNr));
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogPCFrameStatusLimited(string name, Logger.Status status, uint capturerID, uint frameNr)
    {
        if (!loggerSettings.pointCloud.limitLogging || (frameNr % loggerSettings.pointCloud.everyNFrames == 0))
        {
            LogPCFrameStatus(name, status, capturerID, frameNr);
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogPCFrameStatusLimited(string name, Logger.Status status, uint clientID, uint capturerID, uint frameNr)
    {
        if (!loggerSettings.pointCloud.limitLogging || (frameNr % loggerSettings.pointCloud.everyNFrames == 0))
        {
            LogPCFrameStatus(name, status, clientID, capturerID, frameNr);
        }
    }

    [Conditional("ENABLE_LOGGING")]
    public static void LogPCFrameStatusSizeLimited(string name, Logger.Status status, uint capturerID, uint frameNr, uint size)
    {
        if (!loggerSettings.pointCloud.limitLogging || (frameNr % loggerSettings.pointCloud.everyNFrames == 0))
        {
            Log($"{LogCommonEnc(name, status, capturerID, frameNr)} size={size}");
        }
    }
    #endregion
}
