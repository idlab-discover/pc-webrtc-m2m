using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[PipelineRemoteRegister("mdc")] // TODO Change this to pipelineremotepc register
public class PipelineRemotePointcloudMDC : PipelineRemotePointcloudBase
{
    private List<MDCPointCloudReceiverSingle> receivers = new();

    public override void Init(SessionInfo sessionInfo, RemoteConnectedClient remoteClient)
    {
        base.Init(sessionInfo, remoteClient);
        Dictionary<uint, List<RemoteTrackInfo>> capturersToDescNumbers = new();
        foreach (var track in tracks)
        {
            string[] tokens = track.Value.trackID.Split("_");
            if(tokens.Length < 3)
            {
                Debug.LogError("Track ID does not contain enough tokens to identify capturer and description number.");
                continue;
            }
            if (tokens[0] != "mdc")
            {
                Debug.LogError("Track ID does not start with 'mdc'.");
                continue;
            }
            uint capturerID = uint.Parse(tokens[1]);
            uint descriptionNumber = uint.Parse(tokens[2]);
            if (!capturersToDescNumbers.ContainsKey(capturerID))
            {
                capturersToDescNumbers[capturerID] = new();
            }
            capturersToDescNumbers[capturerID].Add(track.Value);
        }
        foreach(var capturer in capturersToDescNumbers)
        {
            MDCPointCloudReceiverSingle receiver = new(capturer.Value, playbackBuffer);
            receivers.Add(receiver);
        }
    }

}
