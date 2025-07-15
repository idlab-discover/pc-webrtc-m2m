using FMODUnity;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

public class RawReceiverPipeline : IDisposable
{
    private readonly RawFrameBuffer playbackBuffer;
    private DepthDecoder depthDecoder;
    private ColorDecoder colorDecoder;
    private RawConverter converter;
    private CaptureType capType;
    private uint clientID;
    private uint capturerID;
    private bool disposedValue;
    private Dictionary<UInt32, DecodedRawFrameSingle> inProgessSingles = new();
    private Mutex mut = new Mutex();

    public RawReceiverPipeline(RawFrameBuffer playbackBuffer, DepthCodecType dType, ColorCodecType cType, CaptureType capType, uint clientID, uint capturerID)
    {
        this.playbackBuffer = playbackBuffer;
        this.capType = capType;
        this.clientID = clientID;
        this.capturerID = capturerID;
        depthDecoder = new DepthDecoder(dType, clientID, capturerID);
        colorDecoder = new ColorDecoder(cType, clientID, capturerID);
    }
    public void OnCalibrationReceived(IntPtr cal)
    {
        mut.WaitOne();
        if(converter != null)
        {
            converter.Dispose();
        }
        converter = new RawConverter(this.capType, cal, this.clientID, this.capturerID);
        mut.ReleaseMutex();
    }
    public void OnDepthReceived(IntPtr dataPtr, uint frameNr, ulong timestamp, uint nPoints, uint width, uint height, uint dataSize)
    {
        IntPtr data = depthDecoder.DecodeDepth(dataPtr, width, height, frameNr);

        mut.WaitOne();
        bool succes = inProgessSingles.TryGetValue((uint)frameNr, out DecodedRawFrameSingle single);
        if (!succes)
        {
            single = new DecodedRawFrameSingle(nPoints, frameNr, nPoints);
            inProgessSingles.Add(frameNr, single);
        }
        single.DecodedDepth = data;
        single.DepthCompleted = true;
        if(single.IsCompleted)
        {
            playbackBuffer.AddSingleAndConvert(single, converter);
        }
        mut.ReleaseMutex();
    }

    public void OnColorReceived(IntPtr dataPtr, uint frameNr, ulong timestamp, uint nPoints, uint width, uint height, uint dataSize)
    {
        IntPtr data = colorDecoder.DecodeColor(dataPtr, dataSize, width, height, frameNr);

        mut.WaitOne();
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
        mut.ReleaseMutex();
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
