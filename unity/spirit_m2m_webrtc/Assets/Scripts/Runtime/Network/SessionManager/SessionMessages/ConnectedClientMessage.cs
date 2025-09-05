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
    public List<ReceivingTrackInfo> receivingTracks = new();
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
    public JObject trackSettings;

    [NonSerialized]
    public TrackStatus status = TrackStatus.NotStarted;

}

[System.Serializable]
public class LocalTrackInfo : ReceivingTrackInfo 
{
    [NonSerialized]
    public NetworkSenderBase Sender;

}

[Serializable]
public class  RemoteTrackInfo : ReceivingTrackInfo
{
    [NonSerialized]
    public NetworkReceiverBase Receiver;
    // TODO Make constructor and call receiver to add the track

    public void StartPollingTrack(NetworkReceiverBase.OnStreamDataReceivedCb cb)
    {
        if (Receiver == null)
        {
            // DO something 
            return;
        }
        if(!Receiver.IsValid)
        {
            // DO Something
        }
    
        Receiver.StartPollVideoTrack(clientID, trackID, cb);
    }
}

