using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.VisualScripting;
using UnityEngine;
using Debug = UnityEngine.Debug;

public class SessionManager : MonoBehaviour
{
    public int LoggingLevel = 0;
    public int ClientID = 0;
    public int NDescriptions = 3;
    public int PeerUDPPort = 8000;
    public string PeerSFUAddress = "192.168.10.2:8094";

    private Process peerProcess;

    // ################## GameObjects ####################
    public List<GameObject> StartLocations;
    public PCSelf PCSelfPrefab;
    public PCReceiver PCReceiverPrefab;
    public GameObject Table;

    // ################# Private Variables ###############
    private PCSelf pcSelf;
    private List<PCReceiver> pcReceivers = new();
    // Start is called before the first frame update

    private SessionInfo sessionInfo;

    void Start()
    {
        sessionInfo = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        ClientID = sessionInfo.clientID;
        Table.transform.position = new Vector3(sessionInfo.table.position.x, sessionInfo.table.position.y, sessionInfo.table.position.z);
        Table.transform.localScale = new Vector3(sessionInfo.table.scale.x, sessionInfo.table.scale.y, sessionInfo.table.scale.z);
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
        for(int i = 0; i < StartLocations.Count; i++)
        {
            if(ClientID == i) // This user
            {
                //StartLocations[i].transform.position = new Vector3(StartLocations[i].transform.position.x, StartLocations[i].transform.position.y - 1, StartLocations[i].transform.position.z);
                StartLocations[i].transform.position = new Vector3(sessionInfo.startPositions[i].x, sessionInfo.startPositions[i].y - 1, sessionInfo.startPositions[i].z);
                pcSelf = Instantiate(PCSelfPrefab, StartLocations[i].transform.position, StartLocations[i].transform.rotation);
                pcSelf.transform.parent = StartLocations[i].transform;
                pcSelf.UseCam = sessionInfo.useCam;
                pcSelf.CamClose = sessionInfo.camClose;
                pcSelf.CamFar = sessionInfo.camFar;
                pcSelf.CamFPS = sessionInfo.camFPS;
                pcSelf.CamWidth = sessionInfo.camWidth;
                pcSelf.CamHeight = sessionInfo.camHeight;
                
            } else // Other users
            {
                StartLocations[i].transform.position = new Vector3(sessionInfo.startPositions[i].x, sessionInfo.startPositions[i].y, sessionInfo.startPositions[i].z);
                PCReceiver pcReceiver = Instantiate(PCReceiverPrefab, StartLocations[i].transform.position, StartLocations[i].transform.rotation);
                pcReceiver.transform.parent = StartLocations[i].transform;
                pcReceiver.ClientID = (uint)i;
                pcReceiver.NDescriptions = NDescriptions;
                pcReceivers.Add(pcReceiver);
                PCHelperWrapper.Receivers.Add(pcReceiver.ClientID,  pcReceiver);
            }
        }
    }
}
