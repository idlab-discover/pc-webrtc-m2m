using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WebSocketSessionManagerMessage 
{
    public string messageType;
    public JObject message;
    public static WebSocketSessionManagerMessage CreateFromJSON(string data)
    {
        return JsonConvert.DeserializeObject<WebSocketSessionManagerMessage>(data);
    }
}

[System.Serializable]
public class WebSocketSessionManagerMessageObject
{
    public string messageType;
    public object message;
    public static WebSocketSessionManagerMessage CreateFromJSON(string data)
    {
        return JsonConvert.DeserializeObject<WebSocketSessionManagerMessage>(data);
    }
}


[System.Serializable]
public class ClientConnectedSettings
{
    public uint clientID;
    public string authKey;
  
}


[System.Serializable]
public class ClientAddedToProviderMessage
{
    public string providerKey;
    public string providerType;
    public string providerAuthKey;
    public string address;
    public uint port;
    public JObject config;
    public List<TrackSimple> senderVideoTracks = new();
    public List<TrackSimple> senderAudioTracks = new();
    public List<RemoteClientSimple> remoteClients = new();
}
[System.Serializable]
public class TrackSimple
{
    public string trackID;
    public bool isConnected;
}

[System.Serializable]
public class RemoteClientSimple
{
    public uint clientID;
    public List<TrackSimple> videoTracks = new();
    public List<TrackSimple> audioTracks = new();
}
/*
 type ClientAddedToProviderMessage struct {
	ProviderType      string                 `json:"providerType"`
	ProviderKey       string                 `json:"providerKey"`
	Address           string                 `json:"address"`
	Port              uint                   `json:"port"`
	Config            map[string]interface{} `json:"config"`
	SenderVideoTracks []TrackSimple          `json:"senderVideoTracks"`
	SenderAudioTracks []TrackSimple          `json:"senderAudioTracks"`
	RemoteClients     []RemoteClientSimple   `json:"remoteClients"`
}
 
 
 */

/*
 type TrackSimple struct {
	TrackID     string `json:"trackID"`
	IsConnected bool   `json:"isConnected"`
}
*/
/*
 type RemoteClientSimple struct {
	ProviderKey string        `json:"providerKey"`
	ClientID    uint          `json:"clientID"`
	VideoTracks []TrackSimple `json:"videoTracks"`
	AudioTracks []TrackSimple `json:"audioTracks"`
}
 */