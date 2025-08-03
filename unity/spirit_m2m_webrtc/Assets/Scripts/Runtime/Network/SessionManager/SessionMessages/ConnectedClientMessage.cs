using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
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

[System.Serializable]
public class ReceivingTrackInfo
{
    public string providerKey;
    public string trackID;
    public string capturerType;
    public string trackType; // Raw, PointCloud etc...
    public JObject trackSettings;
}

