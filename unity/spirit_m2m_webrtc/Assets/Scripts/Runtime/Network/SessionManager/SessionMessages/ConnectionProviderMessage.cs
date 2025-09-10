using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/*
 * {
 *          key:    XXX
 *          type:   XXX
 *          providerSettings:   (as json)
 *          sendingTracks:  [
 *              {
 *                  capturerID:     XXX
 *                  descriptionID:  XXX
 *              },
 *              ...
 *          ]
 * },
 */
[System.Serializable]
public class ConnectionProviderMessage
{
    public string providerKey;
    public string providerType;
    public JObject providerSettings;
    public List<ReceivingTrackInfo> videoTracks = new();

}

[System.Serializable]
public class SendingTrackInfo
{
    public string trackID;
    public string capturerType;
    public string trackType; // Raw, PointCloud etc...
    public string trackSettings;
}
