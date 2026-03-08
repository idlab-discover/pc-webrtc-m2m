using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

public class BenchGenerateMDC : PipelineLocalPointcloudBase
{
    private BenchGenerateMDCConfig benchConfig;
    public string ConfigPath;
    protected override string NAME => "PipelineLocalPointcloudMDC";
    private bool pollNextFrame = false;
    private uint descDoneCount = 0;
    private uint nDesc = 3; // TODO make this configurable
    private MDCEncodingQueue encodingQueue; // TODO move this to video caputre
    private SessionInfo sessionInfo;
    // TODO Add audio capture 
    private readonly object _lock = new();

    protected override FrameMode FrameMode => FrameMode.RealData;

    void Start()
    {
        benchConfig = BenchGenerateMDCConfig.CreateFromJSON(ConfigPath);
        sessionInfo = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        Logger.Init(sessionInfo.loggerSettings);
        startCaptureThread = false; // We want to control when the capture starts
        Init(sessionInfo, new LocalConnectedClient(0, ""));
    }

    public override void Init(SessionInfo sessionInfo, LocalConnectedClient localClient)
    {
        sessionInfo.frameMode = FrameMode.RealData; // TODO fix this in the future
        base.Init(sessionInfo, localClient);
        encodingQueue = new MDCEncodingQueue(sessionInfo);
        encodingQueue.SetDescriptionDoneCallback(OnDescriptionDoneCallback);
        pollNextFrame = true;
        startPollThread();
    }

    protected override void pollFramesInternal()
    {
        lock (_lock)
        {
            while (!pollNextFrame && keepWorking)
            {
                Monitor.Wait(_lock);
            }
        }

        if (!keepWorking)
        {
            encodingQueue.Dispose();
            return;
        }
        
        pollNextFrame = false;
        descDoneCount = 0;
        IntPtr frame = capture.GetSingleCombinedPointCloud();
        if (frame != IntPtr.Zero)
        {
            if(benchConfig.downsampleToNPoints > 0)
            {
                Realsense2Invoker.downsample_pc_random(frame, benchConfig.downsampleToNPoints, false);
            }
            encodingQueue.EncodePointCloud(frame);
        }
        else
        {
            keepWorking = false;
            encodingQueue.Dispose(); // TODO make cleanup cleaner
        }
        
    }

    // TODO move this to mdc encoder queue class
    private void OnDescriptionDoneCallback(EncodedMDCDescription desc)
    {
        if (keepWorking)
        {
            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.EndEncodingPC, LocalClient.ClientID, desc.Header.FrameNr, desc.Header.ToString());
            if(desc.Header.FrameNr % 100 == 0)
            {
                Debug.Log($"{desc.Header.FrameNr} {desc.Header.DescriptionNr} {desc.Header.DataBufferSize}");
            }
            if(benchConfig != null && benchConfig.outputPath != null && benchConfig.outputPath != "")
            {
                // Build the directory path
                string dirPath = $"{benchConfig.outputPath}/{benchConfig.subDirectoryPrefix}_{desc.Header.CapturerID}_{desc.Header.DescriptionNr}";
                // Ensure the directory exists
                if (!System.IO.Directory.Exists(dirPath))
                {
                    System.IO.Directory.CreateDirectory(dirPath);
                }
                // Save desc.Bytes to file with name frame_XXXX.bin where XXXX is the zero-padded frame number
                string fileName = $"frame_{desc.Header.FrameNr.ToString("D4")}.bin";
                string filePath = System.IO.Path.Combine(dirPath, fileName);
                try
                {
                    System.IO.File.WriteAllBytes(filePath, desc.Bytes);
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Failed to write frame to {filePath}: {ex.Message}");
                }
                if(benchConfig.validateEncoding)
                {
                    validateEncoding(filePath, desc);
                }
            }
            

            Logger.LogPCFrameStatusWithMessageLimited(NAME, Logger.Status.FrameAddedToSender, LocalClient.ClientID, desc.Header.FrameNr, desc.Header.ToStringSmall()); // TODO This is currently still using capturerID, change this to reflect it better
        }
        uint frameNr = desc.Header.FrameNr;
        desc.Dispose();
        descDoneCount++;
        if (descDoneCount >= nDesc)
        {
            lock (_lock)
            {
                if (frameNr >= benchConfig.maxFrames) { 
                    keepWorking = false;
                } else
                {
                    pollNextFrame = true;
                }
                Monitor.Pulse(_lock);
            }
        }
    }

    private void validateEncoding(string filePath, EncodedMDCDescription desc)
    {
        validateFromBuffer(System.IO.File.ReadAllBytes(filePath), desc);
    }

    private MDCFrameHeader validateFromBuffer(byte[] buffer, EncodedMDCDescription encDesc)
    {
        var timestampField = BitConverter.ToUInt64(buffer, 0);
        var capturerID = BitConverter.ToUInt32(buffer, 8);
        var frameNrField = BitConverter.ToUInt32(buffer, 12);
        var descriptionNr = BitConverter.ToUInt32(buffer, 16);
        var codecType = BitConverter.ToUInt32(buffer, 20);
        var totalPoints =BitConverter.ToUInt32(buffer, 24);

        IntPtr unmanagedPointer = Marshal.AllocHGlobal(buffer.Length - 28);
        Marshal.Copy(buffer, 28, unmanagedPointer, buffer.Length - 28);
        MDCFrameHeader header = new(
            dataBuffer: unmanagedPointer,
            dataBufferSize: (uint)(buffer.Length - 28),
            timestamp: timestampField,
            capturerID: capturerID, 
            frameNr: frameNrField,
            descriptionNr: descriptionNr, 
            codecType: codecType, 
            numberOfPoints: totalPoints
        );
        DecodedMDCDescription decDesc = new(header, decodeImmediately: true);
        bool invalidFrame = false;
        if (decDesc.NumberOfPoints == 0)
        {
            Debug.LogError($"Validation failed for frame {frameNrField}: more than 0 points, got {decDesc.NumberOfPoints} points. Total PC points in header={totalPoints}");
            invalidFrame = true;
        }
        if(timestampField != encDesc.Header.Timestamp)
        {
            Debug.LogError($"Validation failed for frame {frameNrField}: expected timestamp {encDesc.Header.Timestamp}, got {timestampField}");
            invalidFrame = true;
        }
        if(frameNrField != encDesc.Header.FrameNr)
        {
            Debug.LogError($"Validation failed for frame {frameNrField}: expected frame number {encDesc.Header.FrameNr}, got {frameNrField}");
            invalidFrame = true;
        }
        if(!invalidFrame)
        {
            Debug.Log($"Validation succeeded for frame {frameNrField}");
        }
        decDesc.Dispose();
        Marshal.FreeHGlobal(unmanagedPointer);
        return header;
    }

    protected override void cleanup()
    {
        base.cleanup();
        lock (_lock) {
            Debug.Log("Cleanup pulse");
            pollNextFrame = true;
            Monitor.Pulse(_lock);
        }
        //encodingQueue.Dispose();
    }
}
