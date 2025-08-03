using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RawReceiverSingle : IDisposable
{
    private enum RawDescriptionID
    {
        Depth = 0,
        Color = 1,
    }
    private readonly object _lock = new();
    private readonly NetworkStreamerBase streamer;
    private readonly RawFrameBuffer playbackBuffer;
    private readonly CaptureType capType;
    private readonly uint clientID;
    private readonly uint capturerID;

    private readonly DepthDecoder depthDecoder;
    private readonly ColorDecoder colorDecoder;
    private RawConverter converter;
    private Dictionary<UInt32, DecodedRawFrameSingle> inProgessSingles = new();

    private bool disposedValue;

    public RawReceiverSingle(NetworkStreamerBase streamer, RawFrameBuffer playbackBuffer, DepthCodecType dType, ColorCodecType cType, CaptureType capType, uint clientID, uint capturerID)
    {
        this.streamer = streamer;
        streamer.StartPollVideoTrack(clientID, capturerID, (uint)RawDescriptionID.Depth, onDepthBufferReceived);
        streamer.StartPollVideoTrack(clientID, capturerID, (uint)RawDescriptionID.Color, onColorBufferReceived);
        this.playbackBuffer = playbackBuffer;
        this.capType = capType;
        this.clientID = clientID;
        this.capturerID = capturerID;
        depthDecoder = new DepthDecoder(dType, clientID, capturerID);
        colorDecoder = new ColorDecoder(cType, clientID, capturerID);
    }
    
    public void OnCalibrationReceived(IntPtr cal)
    {
        lock(_lock)
        {
            if (converter != null)
            {
                converter.Dispose();
            }
            converter = new RawConverter(this.capType, cal, this.clientID, this.capturerID);
        }
    }

    private void onDepthBufferReceived(IntPtr data, uint size)
    {
        unsafe
        {
            RawFrameHeader header = new RawFrameHeader(data);
            if(header.CodecType != (uint)depthDecoder.CodecType)
            {
                // TODO error logging
                return;
            }
            onDepthReceived((data + 32), header.FrameNr, header.Timestamp, header.NPoints, header.Width, header.Height, header.EncodedSize);
        }
    }
    private void onColorBufferReceived(IntPtr data, uint size)
    {
        unsafe
        {
            RawFrameHeader header = new RawFrameHeader(data);
            if (header.CodecType != (uint)colorDecoder.CodecType)
            {
                // TODO error logging
                return;
            }
            onColorReceived((data + 32), header.FrameNr, header.Timestamp, header.NPoints, header.Width, header.Height, header.EncodedSize);
        }
    }

    private void onDepthReceived(IntPtr dataPtr, uint frameNr, ulong timestamp, uint nPoints, uint width, uint height, uint dataSize)
    {
        IntPtr data = depthDecoder.DecodeDepth(dataPtr, width, height, frameNr);

        lock(_lock)
        {
            bool succes = inProgessSingles.TryGetValue((uint)frameNr, out DecodedRawFrameSingle single);
            if (!succes)
            {
                single = new DecodedRawFrameSingle(nPoints, frameNr, nPoints);
                inProgessSingles.Add(frameNr, single);
            }
            single.DecodedDepth = data;
            single.DepthCompleted = true;
            if (single.IsCompleted)
            {
                playbackBuffer.AddSingleAndConvert(single, converter);
            }
        }
        
        
    }
 

    private void onColorReceived(IntPtr dataPtr, uint frameNr, ulong timestamp, uint nPoints, uint width, uint height, uint dataSize)
    {
        IntPtr data = colorDecoder.DecodeColor(dataPtr, dataSize, width, height, frameNr);

        lock(_lock)
        {
            bool succes = inProgessSingles.TryGetValue((uint)frameNr, out DecodedRawFrameSingle single);
            if (!succes)
            {
                single = new DecodedRawFrameSingle(nPoints, frameNr, nPoints);
                inProgessSingles.Add(frameNr, single);
            }
            single.ColorWidth = width;
            single.ColorHeight = height;
            single.DecodedColor = data;
            single.ColorsCompleted = true;
            if (single.IsCompleted)
            {
                playbackBuffer.AddSingleAndConvert(single, converter);
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }
            if (depthDecoder != null)
            {
                depthDecoder.Dispose();
            }
            if (colorDecoder != null)
            {
                colorDecoder.Dispose();
            }
            if (converter != null)
            {
                converter.Dispose();
            }
            disposedValue = true;
        }
    }

    public void Dispose()
    {
        // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}
