using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[PipelineRemoteRegister("mdc")] // TODO Change this to pipelineremotepc register
public class PipelineRemotePointcloudMDC : PipelineRemotePointcloudBase
{
    protected override string NAME => "PipelineRemotePointcloudMDC";
    private List<MDCPointCloudReceiverSingle> receivers = new();

    public override void Init(SessionInfo sessionInfo, RemoteConnectedClient remoteClient)
    {
        base.Init(sessionInfo, remoteClient);
        Dictionary<uint, List<RemoteTrackInfo>> capturersToDescNumbers = new();
        foreach (var track in tracks)
        {
            string[] tokens = track.Value.trackID.Split("_");
            if(tokens.Length < 4)
            {
                Debug.LogError($"Track ID {track.Value.trackID} does not contain enough tokens to identify capturer and description number.");
                continue;
            }
            if (tokens[1] != "mdc")
            {
                Debug.LogError($"Track ID {track.Value.trackID} does not start with 'mdc'.");
                continue;
            }
            uint capturerID = uint.Parse(tokens[2]);
            uint descriptionNumber = uint.Parse(tokens[3]);
            if (!capturersToDescNumbers.ContainsKey(capturerID))
            {
                capturersToDescNumbers[capturerID] = new();
            }
            capturersToDescNumbers[capturerID].Add(track.Value);
        }
        foreach(var capturer in capturersToDescNumbers)
        {
            MDCPointCloudReceiverSingle receiver = new(capturer.Value, playbackBuffer, RemoteClient.ClientID);
            receivers.Add(receiver);
        }
    }

}
