using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class ProviderTracksConnectedMessage : MonoBehaviour
{
	public string providerKey;
	public uint clientID;
	public List<TrackSimple> videoTracks = new List<TrackSimple>();
	public List<TrackSimple> audioTracks = new List<TrackSimple>();
}
