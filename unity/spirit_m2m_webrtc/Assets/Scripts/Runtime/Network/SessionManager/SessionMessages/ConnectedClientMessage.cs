using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

/*
 * {
 *          clientID:   XXX,
 *          clientSettings: (as json){
 *              nDescriptions:  XXX
 *              nCapturers:     XXX
 *          }
 *          receivingTracks:    [
 *              {
 *                  providerKey:    XXX
 *                  capturerID:     XXX
 *                  descriptionID:  XXX
 *                  pipelineSettings:  XXX
 *              }
 *          ]
 * }
*/
[System.Serializable]
public class ConnectedClientMessage 
{
    public uint clientID;
    public string codecMode;
    public string clientSettings;
    public string pipelineType;
    // public List<ReceivingTrackInfo> receivingTracks = new();
    public List<ReceivingTrackInfo> videoTracks = new();
    public List<ReceivingTrackInfo> audioTracks = new();
}

public enum TrackStatus
{
    NotStarted = 0,
    Started = 1,
    Paused = 2,
    Stopped = 3,
    Error = 4
}

[System.Serializable]
public class ReceivingTrackInfo
{
    public uint clientID;
    public string providerKey;
    public string trackID;
    public string capturerType;
    public string trackType; // Raw, PointCloud etc...
    public bool isVideo = true;
    public JObject trackSettings;

    [NonSerialized]
    public TrackStatus status = TrackStatus.NotStarted;

    [NonSerialized]
    protected readonly object _lock = new();

}

[System.Serializable]
public class LocalTrackInfo : ReceivingTrackInfo 
{
    public NetworkSenderBase Sender { get; private set; }
    public void SetSender(NetworkSenderBase sender) 
    {
        lock(_lock)
        {
            Sender = sender;
        }
    }
}

[Serializable]
public class  RemoteTrackInfo : ReceivingTrackInfo
{
    public NetworkReceiverBase Receiver { get; private set; }
    // TODO Make constructor and call receiver to add the track
    private NetworkReceiverBase.OnStreamDataReceivedCb tempCb;

    public void StartPollingTrack(NetworkReceiverBase.OnStreamDataReceivedCb cb)
    {
        lock (_lock)
        {
            if (Receiver == null || !Receiver.IsValid)
            {
                tempCb = cb;
                return;
            }
            Receiver.StartPollVideoTrack(clientID, trackID, cb);
        }
    }

    public void SetReceiver(NetworkReceiverBase receiver) { 
        lock(_lock)
        {
            Receiver = receiver;
            if (tempCb != null && Receiver != null && Receiver.IsValid)
            {
                Receiver.StartPollVideoTrack(clientID, trackID, tempCb);
                tempCb = null;
            }
        }
    }
}

