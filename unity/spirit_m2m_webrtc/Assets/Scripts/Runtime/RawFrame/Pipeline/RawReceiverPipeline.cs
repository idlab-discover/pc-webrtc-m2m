using FMODUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class RawReceiverPipeline : RenderablePointCloudPipeline
{
    private readonly object _lock = new();
    private Dictionary<uint, RawReceiverSingle> receivers = new();

    public RawReceiverPipeline(PlaybackBufferSettings settings, uint clientID, uint targetFPS) : base(new RawFrameBuffer(settings, 0, targetFPS)/* TODO change to use parameters */, clientID)
    {
     
    }
    
    public override void OnVideoTrackAdded(NetworkStreamerBase streamer, uint capturerID, uint descriptionID, CaptureType capType, string trackInfo)
    {
        if(receivers.ContainsKey(capturerID))
        {
            OnVideoTrackRemoved(capturerID, descriptionID);
        }
        
        RawReceiverTrackInfo settings = RawReceiverTrackInfo.CreateFromJSON(trackInfo);
        ColorCodecType colorCodecType = ColorCodecHelper.GetTypeForString(settings.colorCodecName);
        DepthCodecType depthCodecType = DepthCodecHelper.GetTypeForString(settings.depthCodecName);
        if(colorCodecType == ColorCodecType.Invalid || depthCodecType == DepthCodecType.Invalid)
        {
            // TOOD some error message
            return;
        }
        lock(_lock)
        {
            ((RawFrameBuffer)playbackBuffer).NActiveCapturers++;
            receivers[capturerID] = new RawReceiverSingle(streamer, (RawFrameBuffer)playbackBuffer, depthCodecType, colorCodecType, capType, clientID, capturerID);
        }
        
    }

    public override void OnVideoTrackRemoved(uint capturerID, uint descriptionID)
    {
        lock(_lock)
        {
            bool deleted = receivers.Remove(capturerID, out RawReceiverSingle receiver);
            if(!deleted)
            {
                return;
            }
            receiver.Dispose();
            ((RawFrameBuffer)playbackBuffer).NActiveCapturers--;
        }
        
    }

    protected override void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }

            disposedValue = true;
        }
    }


}
