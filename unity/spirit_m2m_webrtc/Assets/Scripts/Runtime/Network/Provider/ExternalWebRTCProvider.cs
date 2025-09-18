using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEngine;

[ConnectionProviderRegister("webrtc_sfu")]
public class ExternalWebRTCProvider : ConnectionProviderBase, ISenderSupported, IReceiverSupported
{
    private Process peerProcess;
    public ExternalWebRTCProvider(string ID, string ip, uint port, JObject jsonSettings) : base(ID, ip, port, jsonSettings)
    {
        //Logger.
        var settings = jsonSettings.ToObject<ExternalWebRTCSettings>();

      //  WebRTCInvoker.initialize("127.0.0.1", (uint)sessionInfo.peerUDPPort, "127.0.0.1", (uint)sessionInfo.peerUDPPort, 1, (uint)NDescriptions, (uint)ClientID, "1.0");
        
        // TODO Make this portable to other OS
        /*peerProcess = new Process();
        peerProcess.StartInfo.FileName = Application.dataPath + "/peer/webRTC-peer-win.exe";
        peerProcess.StartInfo.Arguments = $"-p :{settings.peerUDPPort} -i -o -sfu {sessionInfo.sfuAddress} -c {ClientID} -cam {1} -t {settings.nMaxTracks}";
        peerProcess.StartInfo.CreateNoWindow = false;
        //  if (peerInWindow && peerWindowDontClose)
        //   {
        peerProcess.StartInfo.Arguments = $"/K {peerProcess.StartInfo.FileName} {peerProcess.StartInfo.Arguments}";
        peerProcess.StartInfo.FileName = "CMD.EXE";
        //  }*/


        // Init WebRTC


       /* if (!peerProcess.Start())
        {
            Debug.LogError("Failed to start peer process");
            peerProcess = null;
            return;
        }*/
    }

    protected override string NAME => "ExternalWebRTCProvider";

    public override bool IsReady()
    {
        throw new System.NotImplementedException();
    }

    protected override void connectInternal()
    {
        throw new System.NotImplementedException();
    }

    protected override void disconnectInternal()
    {
        throw new System.NotImplementedException();
    }

    protected override void Dispose(bool disposing)
    {
        throw new System.NotImplementedException();
    }

    public NetworkReceiverBase GetReceiver(ReceivingTrackInfo track, uint clientID)
    {
        throw new System.NotImplementedException();
    }

    public NetworkSenderBase GetSender(ReceivingTrackInfo track)
    {
        throw new System.NotImplementedException();
    }
}
