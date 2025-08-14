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

        // SessionManager Status
        ManagerConnectionStart = 1000,
        ManagerConnectionSuccess = 1001,
        ManagerConnectionFailed = 1002,
        ManagerConnectionClose = 1003,
        ManagerProviderRequested = 1010,
        ManagerProviderRemoved = 1011,
        ManagerProviderChange = 1012,
        ManagerClientConnected = 1020,
        ManagerClientDisconnected = 1021,
        ManagerSessionCreating = 1030,
        ManagerSessionCreated = 1031,
        ManagerSessionJoining = 1032,
        ManagerSessionJoined =  1033,
        ManagerSessionLeft = 1034,
        ManagerSessionRejoined = 1035,
        ManagerSessionClosed = 1036,

        // ConnectionProvider Status
        ProviderConnectionStart = 2000,
        ProviderConnectionSuccess = 2001,
        ProviderConnectionFailed = 2002,
        ProviderConnectionClose = 2003,
        ProviderNotFound = 2004,
        ProviderSenderNotSupported = 2005,
        ProviderReceiverNotSupported = 2006,

        // Client Status
        ClientAddVideoTrack = 3000,
        ClientRemoveVideoTrack = 3001,
        ClientAddAudioTrack = 3002,
        ClientRemoveAudioTrack = 3003,
        ClientTrackAlreadyExists = 3004,
        ClientTrackNotFound = 3005,
        ClientTrackSenderNull = 3006,
        ClientTrackSenderInvalid = 3007,
        ClientTrackSenderNotReady = 3008,
        ClientTrackReceiverNull = 3009,
        ClientTrackReceiverInvalid = 3010,
        ClientTrackReceiverNotReady = 3011,

        // TrackInfo Status
        GatheringTrackInfo = 4000,
        NewTrackDiscovered = 4001,

        // Misc Status
        FactoryCreate = 8000,
        FactoryCreateSucces = 8001,
        FactoryCreateFailed = 8002,
        // Debugging Status
        StartRawColorCopy = 9000,
        EndRawColorCopy = 9001,
        EnterLock = 9002,
        ExitLock = 9003,
    }
    public static long Time => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public static string FilePath { get; private set; }
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
        FilePath = loggerSettings.logPath;
        if (loggerSettings.appendTimestampToPath)
        {
            FilePath += "_" + DateTime.Now.ToString("yyyyMMdd_HH-mm-ss");
        }
        FilePath += ".txt";
        writer = new StreamWriter(FilePath, false);
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
    private static string LogCommon(string name, Logger.Status status)
    {
#if LOGGER_OUT_STRING
        return $"id={name} ts={Time} status={status}";
#else
        return $"id={name} ts={Time} status={((int)status)}";
#endif 
    } 
    private static string LogCommonClientEnc(string name, Logger.Status status, uint clientID, uint capturerID, uint frameNr) => $"{LogCommonClient(name, status, clientID)} capturerID={capturerID} frameNr={frameNr}";
    private static string LogCommonEnc(string name, Logger.Status status, uint capturerID, uint frameNr) => $"{LogCommon(name, status)} capturerID={capturerID} frameNr={frameNr}";
    private static string LogCommonClient(string name, Logger.Status status, uint clientID) => $"{LogCommon(name, status)} clientID={clientID}";

    private static string LogCommonTrackStatus(string name, Logger.Status status, uint clientID, string trackID) => $"{LogCommon(name, status)} clientID={clientID} trackID={trackID}";
    [Conditional("ENABLE_LOGGING")]
    public static void LogStatus(string name, Logger.Status status)
    {
        Log(LogCommon(name, status));
    }
    [Conditional("ENABLE_LOGGING")]
    public static void LogStatusWithMessage(string name, Logger.Status status, string message)
    {
        Log($"{LogCommon(name, status)} {message}");
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

    [Conditional("ENABLE_LOGGING")]
    public static void LogTrackStatus(string name, Logger.Status status, uint clientID, string trackID)
    {
        Log(LogCommonTrackStatus(name, status, clientID, trackID));
    }
    [Conditional("ENABLE_LOGGING")]
    public static void LogTrackStatusWithProvider(string name, Logger.Status status, uint clientID, string trackID, string provider)
    {
        Log($"{LogCommonTrackStatus(name, status, clientID, trackID)} provider={provider}");
    }
    #endregion
}
