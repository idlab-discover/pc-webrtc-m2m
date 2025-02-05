using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class SessionManagerAudioTest : MonoBehaviour
{
    public int LoggingLevel = 0;
    public int ClientID = 0;
    public int NDescriptions = 3;
    public int PeerUDPPort = 8000;
    public string PeerSFUAddress = "127.0.0.1:8094";

    private Process peerProcess;

    // ################## GameObjects ####################
    public List<GameObject> StartLocations;
    public AudioCapture Cap;
    public GameObject Table;
    public AudioPlayback PlaybackPrefab;
    public int NPlayback;
    List<AudioPlayback> play;
    private List<System.Threading.Thread> workerThreads = new List<System.Threading.Thread>();
    // ################# Private Variables ###############
    // Start is called before the first frame update
    private bool keep_working = true;
    private SessionInfo sessionInfo;

    void Start()
    {
        workerThreads=new List<System.Threading.Thread>();
        play =new List<AudioPlayback>();
        sessionInfo = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        ClientID = sessionInfo.clientID;
        //Table.transform.position = new Vector3(sessionInfo.table.position.x, sessionInfo.table.position.y, sessionInfo.table.position.z);
       // Table.transform.localScale = new Vector3(sessionInfo.table.scale.x, sessionInfo.table.scale.y, sessionInfo.table.scale.z);
        // Init DLLs for logging
        DLLWrapper.LoggingInit(LoggingLevel);
        // TODO Start peer
        WebRTCInvoker.initialize("127.0.0.1", (uint)sessionInfo.peerUDPPort, "127.0.0.1", (uint)sessionInfo.peerUDPPort, (uint)NDescriptions, (uint)ClientID, "1.0");

        peerProcess = new Process();
        peerProcess.StartInfo.FileName = Application.dataPath + "/peer/webRTC-peer-win.exe";
        peerProcess.StartInfo.Arguments = $"-p :{sessionInfo.peerUDPPort} -i -o -sfu {sessionInfo.sfuAddress} -c {ClientID} -t {NDescriptions}";
        peerProcess.StartInfo.CreateNoWindow = false;
        //  if (peerInWindow && peerWindowDontClose)
        //   {
        peerProcess.StartInfo.Arguments = $"/K {peerProcess.StartInfo.FileName} {peerProcess.StartInfo.Arguments}";
        peerProcess.StartInfo.FileName = "CMD.EXE";
        //  }*/

        PCHelperWrapper.Init();
        // Init WebRTC


        if (!peerProcess.Start())
        {
            Debug.LogError("Failed to start peer process");
            peerProcess = null;
            return;
        }


        // Make correct prefabs
        Cap.CB = CopyDataToPlayback;
        Cap.Init(sessionInfo.audioPlayback.codecName, sessionInfo.audioPlayback.dspSize);
        for (int i = 0; i < NPlayback; i++)
        {
            AudioPlayback p = Instantiate(PlaybackPrefab);
            p.Init(Cap.CaptureSrate, sessionInfo.audioPlayback);
            play.Add(p);
            workerThreads.Add(new System.Threading.Thread(() =>
            {
                pollAudio((uint)2, p);
            }));
            workerThreads[i].Start();
        }

        CreateStartPrefabs();
    }

    // Update is called once per frame
    void Update()
    {

    }

    void OnApplicationQuit()
    {
        WebRTCInvoker.clean_up();
        peerProcess?.Kill();
    }

    // ############################# Private Functions #######################################
    private void CreateStartPrefabs()
    {
        for (int i = 0; i < StartLocations.Count; i++)
        {
            if (ClientID == i) // This user
            {
                //StartLocations[i].transform.position = new Vector3(StartLocations[i].transform.position.x, StartLocations[i].transform.position.y - 1, StartLocations[i].transform.position.z);
               // StartLocations[i].transform.position = new Vector3(sessionInfo.startPositions[i].x, sessionInfo.startPositions[i].y - 1, sessionInfo.startPositions[i].z);
                /*pcSelf = Instantiate(PCSelfPrefab, StartLocations[i].transform.position, StartLocations[i].transform.rotation);
                pcSelf.transform.parent = StartLocations[i].transform;
                pcSelf.UseCam = sessionInfo.useCam;
                pcSelf.CamClose = sessionInfo.camClose;
                pcSelf.CamFar = sessionInfo.camFar;
                pcSelf.CamFPS = sessionInfo.camFPS;
                pcSelf.CamWidth = sessionInfo.camWidth;
                pcSelf.CamHeight = sessionInfo.camHeight;*/

            }
            else // Other users
            {
              //  StartLocations[i].transform.position = new Vector3(sessionInfo.startPositions[i].x, sessionInfo.startPositions[i].y, sessionInfo.startPositions[i].z);
                /*PCReceiver pcReceiver = Instantiate(PCReceiverPrefab, StartLocations[i].transform.position, StartLocations[i].transform.rotation);
                pcReceiver.transform.parent = StartLocations[i].transform;
                pcReceiver.ClientID = (uint)i;
                pcReceiver.NDescriptions = NDescriptions;
                pcReceivers.Add(pcReceiver);*/
                //PCHelperWrapper.Receivers.Add(pcReceiver.ClientID, pcReceiver);
            }
        }
    }
    void CopyDataToPlayback(byte[] encodedData)
    {
        // byte[] frameHeader = new byte[12];
        //Debug.Log("[AUDIO SENDING]" + lengthElements + " " + );
     /*   byte[] messageBuffer = new byte[12 + data.Length * sizeof(float)];
        Debug.Log("audio send" + messageBuffer.Length);
        long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var timestampField = BitConverter.GetBytes(timestamp);
        timestampField.CopyTo(messageBuffer, 0);
        var frameNrField = BitConverter.GetBytes(frameNr);
        frameNrField.CopyTo(messageBuffer, 8);
        Buffer.BlockCopy(data, 0, messageBuffer, 12, data.Length * sizeof(float));*/
        unsafe
        {
            fixed (byte* bufferPointer = encodedData)
            { 
                WebRTCInvoker.send_audio(bufferPointer, (uint)encodedData.Length);
            }
        }
    }
    private void pollAudio(uint clientID, AudioPlayback pb)
    {
        WebRTCInvoker.wait_for_peer();
        while (keep_working)
        {
            Debug.Log("Polling size");
            int audioSize = WebRTCInvoker.get_audio_size(clientID);

            if (audioSize == 0)
            {
                keep_working = false;
                Debug.Log("Got no tile");
                continue;
            }
            Debug.Log("Got a tile");
            byte[] messageBuffer = new byte[12 + audioSize];
            float[] audioBuffer = new float[4096];
            IntPtr decoderPtr = IntPtr.Zero;
            //int descriptionFrameNr = WebRTCInvoker.get_tile_frame_number(ClientID, descriptionID);
            unsafe
            {
                fixed (byte* ptr = messageBuffer)
                {
                    WebRTCInvoker.retrieve_audio(ptr, (uint)audioSize, clientID);
                    Debug.Log("audio received" + audioSize);
                    UInt64 timestamp = BitConverter.ToUInt64(messageBuffer, 0);
                    uint audioFrameNr = BitConverter.ToUInt32(messageBuffer, 8);
                    Buffer.BlockCopy(messageBuffer, 12, audioBuffer, 0, audioBuffer.Length*sizeof(float));
                    pb.CopyToBuffer(timestamp, audioFrameNr, audioBuffer);

                    // queues[(int)descriptionID].Enqueue(new DecodedPointCloudData(points, colors));
                }
            }
        }
    }
}
