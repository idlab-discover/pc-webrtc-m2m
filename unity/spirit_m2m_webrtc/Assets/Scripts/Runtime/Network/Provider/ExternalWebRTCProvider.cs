using Newtonsoft.Json.Linq;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using UnityEditor.PackageManager;
using UnityEngine;

[ConnectionProviderRegister("webrtc_sfu")]
public class ExternalWebRTCProvider : ConnectionProviderBase, ISenderSupported, IReceiverSupported
{
    protected override string NAME => "ExternalWebRTCProvider";
    private readonly object _lock = new();
    private IntPtr ptr;
    private Process peerProcess;
    private Dictionary<uint, ExternalWebRTCReceiver> receivers = new();
    private ExternalWebRTCSender sender;
    private uint _portSelf;
    private uint _portRemote;
    public ExternalWebRTCProvider(LocalConnectedClient localConnectedClient, ClientAddedToProviderMessage pMsg) : base(localConnectedClient, pMsg)
    {
        //Logger.
        // TODO Load complete WebRTC Config
        // TODO Move external ports outside of webrtc, we can reuse this for other protocols as well probably
        
        ExternalWebRTCPorts.LoadConfig($"{Application.dataPath}/config/session/webrtc_ports.json", false);
        uint? portSelf = ExternalWebRTCPorts.Instance.AcquirePort();
        uint? portRemote = ExternalWebRTCPorts.Instance.AcquirePort();
        if (portSelf == null || portRemote == null)
        {
            Logger.LogStatus(NAME, Logger.Status.ProviderNoFreePort);
            return;
        }

        _portRemote = portRemote.Value;
        _portSelf = portSelf.Value;
        Logger.LogStatusWithMessage(NAME, Logger.Status.Creating, $"portSelf={_portSelf} portRemote={_portRemote}");
        var settings = pMsg.config.ToObject<ExternalWebRTCSettings>();
        ptr = WebRTCInvoker.create_new_webrtc_connection(_portSelf, _portRemote);
        if (ptr == IntPtr.Zero)
        {
            UnityEngine.Debug.LogError("ExternalWebRTCProvider: Ptr is zero");
            Logger.LogStatus(NAME, Logger.Status.Failed);
            return;
        }
        sender = new ExternalWebRTCSender(ptr, pMsg.senderVideoTracks, pMsg.senderAudioTracks); // TODO This will eventually need to be dynamic
        peerProcess = new Process();
        peerProcess.StartInfo.FileName = Application.dataPath + "/peer/peer.exe"; // TODO Read this from config file, maybe add to session config pairs of {providerType, configPath}
        peerProcess.StartInfo.Arguments = $"-i -c {localConnectedClient.ClientID} -p {_portSelf} -r {_portRemote} " +
            $"--vt {sender.VideoTracksString} --sfuKey {pMsg.providerKey} " +
            $"--sfuIP {pMsg.address} --sfuPort {pMsg.port} --sfuAuth TODO";
        UnityEngine.Debug.Log($"-i -c {localConnectedClient.ClientID} -p {_portSelf} -r {_portRemote} --vt {sender.VideoTracksString} --at {sender.AudioTracksString} --sfuKey {pMsg.providerKey} " +
            $"--sfuIP {pMsg.address} --sfuPort {pMsg.port} --sfuAuth TODO");
        peerProcess.StartInfo.CreateNoWindow = false;
        peerProcess.StartInfo.Arguments = $"/K {peerProcess.StartInfo.FileName} {peerProcess.StartInfo.Arguments}";
        peerProcess.StartInfo.FileName = "CMD.EXE";

        if (!peerProcess.Start())
        {
            UnityEngine.Debug.LogError("ExternalWebRTCProvider: Failed to start peer process");
            Logger.LogStatus(NAME, Logger.Status.Failed);
            peerProcess = null;
            return;
        }
        Logger.LogStatus(NAME, Logger.Status.Created);
    }



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
        foreach (var r in receivers.Values)
        {
            r.Dispose();
        }
        ExternalWebRTCPorts.Instance.ReleasePort(_portSelf);
        ExternalWebRTCPorts.Instance.ReleasePort(_portRemote);
        if(ptr != IntPtr.Zero)
        {
            WebRTCInvoker.free_webrtc_connection(ptr);
            ptr = IntPtr.Zero;
        }
        if(peerProcess != null && !peerProcess.HasExited)
        {
            peerProcess.Kill();
            peerProcess = null;
        }
    }

    public NetworkReceiverBase GetReceiver(RemoteTrackInfo track, uint clientID) // Ideally we dont want to use this one, the list one is much better and tested
    {
        if (!receivers.TryGetValue(clientID, out var receiver))
        {
            receiver = new ExternalWebRTCReceiver(WebRTCInvoker.add_client(ptr, clientID));
            lock (_lock)
            {
                receivers[clientID] = receiver;
            }
        }
        receiver.AddTrack(track);
        return receiver;
    }
    public NetworkReceiverBase GetReceiverForTrackList(List<RemoteTrackInfo> tracks, uint clientID)
    {
        if (!receivers.TryGetValue(clientID, out var receiver))
        {
            Logger.LogStatusWithMessage(NAME, Logger.Status.DebugTest, $"func=GetReceiverForTrackList clientID={clientID}");
            receiver = new ExternalWebRTCReceiver(WebRTCInvoker.add_client(ptr, clientID));
            lock (_lock)
            {
                receivers[clientID] = receiver;
            }
        }
        receiver.AddTracks(tracks);
        return receiver;
    }

    public NetworkSenderBase GetSender(ReceivingTrackInfo track)
    {
        return sender;
    }

}
