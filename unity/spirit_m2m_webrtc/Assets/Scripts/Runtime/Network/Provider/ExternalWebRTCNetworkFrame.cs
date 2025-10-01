using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExternalWebRTCNetworkFrame : NetworkFrame
{
    private IntPtr framePtr;
    public ExternalWebRTCNetworkFrame(IntPtr framePtr)
    {
        if(framePtr == IntPtr.Zero)
        {
            throw new ArgumentException("framePtr cannot be null");
        }
        this.framePtr = framePtr;
        this.Size = WebRTCInvoker.get_frame_size(framePtr);
        this.DataPtr = WebRTCInvoker.get_frame_data_ptr(framePtr);
    }
    protected override void disposeInternal()
    {
        if(framePtr != null)
        {
            WebRTCInvoker.free_track_frame(framePtr);
        }
    }
}
