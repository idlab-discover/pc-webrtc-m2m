using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PCHelperWrapper
{
    public static Dictionary<uint, PCReceiver> Receivers;

    [MonoPInvokeCallback(typeof(WebRTCInvoker.trackChangeCb))]
    static void OnTrackChangeCallback(UInt32 clientID, UInt32 lastFrameNr, UInt32 capturerID, UInt32 tileNr, bool isAdded)
    {
        PCReceiver receiver;
        Debug.Log($"Track Capturer: {capturerID} Description: {tileNr} from {clientID} was added {isAdded} after frame {lastFrameNr}");
        if(Receivers.TryGetValue(clientID, out receiver)) {
            receiver.OnTrackChange(lastFrameNr, (int)tileNr, isAdded);
        }
    }

    [MonoPInvokeCallback(typeof(WebRTCInvoker.intrinsicsUpdatedCb))]
    static void OnIntrinsicsUpdatedCallback(UInt32 clientID, UInt32 capturerID, UInt32 capturerType, IntPtr data)
    {
        PCReceiver receiver;
        Debug.Log($"Intrinsics Updated from {clientID} and capturer: {capturerID}");
        if (Receivers.TryGetValue(clientID, out receiver))
        {
            receiver.OnIntrisicsUpdated(capturerID, capturerType, data);
        }
    }

    
    public static void Init()
    {
        Receivers = new();
        WebRTCInvoker.register_track_change_callback(OnTrackChangeCallback);
        WebRTCInvoker.register_intrisics_updated_callback(OnIntrinsicsUpdatedCallback);
    }
}
