using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PCHelperWrapper
{
    public static Dictionary<uint, PCReceiver> Receivers;

    [MonoPInvokeCallback(typeof(WebRTCInvoker.trackChangeCb))]
    static void OnTrackChangeCallback(UInt32 clientID, UInt32 lastFrameNr, UInt32 tileNr, bool isAdded)
    {
        PCReceiver receiver;
        Debug.Log($"Track {tileNr} from {clientID} was added {isAdded} after frame {lastFrameNr}");
        if(Receivers.TryGetValue(clientID, out receiver)) {
            receiver.OnTrackChange(lastFrameNr, (int)tileNr, isAdded);
        }
    }

    public static void Init()
    {
        Receivers = new();
        WebRTCInvoker.register_track_change_callback(OnTrackChangeCallback);
    }
}
