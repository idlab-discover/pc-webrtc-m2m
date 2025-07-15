using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using UnityEngine;

public class RawConverter : IDisposable
{
    const string NAME = "RawConverterUnity";
    public bool IsValid { get; private set; }
    private IntPtr ptr;
    private bool disposedValue;

    private readonly uint clientID;
    private readonly uint capturerID;
    public RawConverter(CaptureType captureType, IntPtr captureCalibration, uint _clientID, uint _capturerID)
    {
        clientID = _clientID;
        capturerID = _capturerID;
        Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Creating, clientID, capturerID);

        ptr = Realsense2Invoker.create_new_raw_converter(captureType, captureCalibration);

        if (ptr == IntPtr.Zero)
        {
            Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Failed, clientID, capturerID);
            IsValid = false;
        }
        else
        {
            Logger.LogStatusClientAndCapturer(NAME, Logger.Status.Created, clientID, capturerID);
            IsValid = true;
        }

    }
    private void convertRawFrameInternal(DecodedRawFrameBase d, Vector3[] targetPoints, Color32[] targetColors)
    {
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.StartRawConversion, clientID, capturerID, d.FrameNr);
        IntPtr buf_depth = RawInvoker.get_decoded_depth_data(d.DecodedDepth);
        IntPtr buf_color = RawInvoker.get_decoded_color_data(d.DecodedColor);
        GCHandle hDepth = GCHandle.Alloc(targetPoints, GCHandleType.Pinned);
        GCHandle hColor = GCHandle.Alloc(targetColors, GCHandleType.Pinned);

        try
        {
            Realsense2Invoker.convert_raw_frame(ptr, buf_depth, buf_color, hDepth.AddrOfPinnedObject(), hColor.AddrOfPinnedObject());
        }
        finally
        {
            hDepth.Free();
            hColor.Free();
        }

#if DEBUG_RAW_FRAME
        d.InitRawColors(d.ColorWidth * d.ColorHeight);
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.StartRawColorCopy, clientID, capturerID, d.FrameNr);
        unsafe
        {
            byte* src = (byte*)buf_color;
            fixed (Color32* dst = d.DecodedColors)
            {
                Color32* colorPtr = dst;
                for (int i = 0; i < d.ColorWidth * d.ColorHeight; i++)
                {
                    colorPtr->r = *src++;
                    colorPtr->g = *src++;
                    colorPtr->b = *src++;
                    colorPtr->a = 255;
                    colorPtr++;
                }
            }
        }
        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.EndRawColorCopy, clientID, capturerID, d.FrameNr);
#endif

        RawInvoker.free_decoded_color(d.DecodedColor); // TODO maybe move to dispose of frame
        RawInvoker.free_decoded_depth(d.DecodedDepth);

        Logger.LogPCFrameStatusLimited(NAME, Logger.Status.EndRawConversion, clientID, capturerID, d.FrameNr);
    }

    public void ConvertRawSingleFrame(DecodedRawFrameMulti m, DecodedRawFrameSingle s)
    {
        if (!IsValid)
        {
            return;
        }
        convertRawFrameInternal(s, m.Points, m.Colors);
    }
    public void ConvertRawFrame(DecodedRawFrame dec, uint width, uint height)
    {
        if (!IsValid)
        {
            return;
        }
        convertRawFrameInternal(dec, dec.Points, dec.Colors);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                // TODO: dispose managed state (managed objects)
            }
            if (IsValid)
            {
                Realsense2Invoker.free_raw_converter(ptr);
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
