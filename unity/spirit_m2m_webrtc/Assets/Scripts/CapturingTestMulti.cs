using AOT;
using Draco;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public class CapturingTestMulti : MonoBehaviour
{
    public List<GameObject> renderers = new List<GameObject>();
    private List<MeshFilter> filters = new List<MeshFilter>();
    public GameObject VRCam;
    public GameObject Table;
    public int ClientID = 0;
    System.Threading.Thread myThread;
    static Dictionary<UInt32, DecodedPointCloudData> inProgessFrames;
    static ConcurrentQueue<DecodedPointCloudData> queue;
    private static Mutex mut = new Mutex();
    Mesh currentMesh;
    //private MeshFilter meshFilter;
    public int debug = 0;

    static bool keep_working = true;
    enum Color { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(DLLLogger.debugCallback))]
    static void OnDebugCallback(IntPtr request, int color, int size)
    {
        // Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);
        // Add specified color
        debug_string =
            String.Format("Realsense Capturing: {0}{1}{2}{3}{4}",
            "<color=",
            ((Color)color).ToString(), ">", debug_string, "</color>");
        // Log the string
        Debug.Log(debug_string);
    }
    [MonoPInvokeCallback(typeof(DLLLogger.debugCallback))]
    static void OnDebugCallbackDraco(IntPtr request, int color, int size)
    {
        // Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);
        // Add specified color
        debug_string =
            String.Format("Draco: {0}{1}{2}{3}{4}",
            "<color=",
            ((Color)color).ToString(), ">", debug_string, "</color>");
        // Log the string
        Debug.Log(debug_string);
    }

    [MonoPInvokeCallback(typeof(DracoInvoker.descriptionDoneCallback))]
    static void OnDescriptionDoneCallback(IntPtr dsc, IntPtr rawDataPtr, UInt32 totalPointsInCloud, UInt32 dscSize, UInt32 frameNr, UInt32 dscNr)
    {
        
        if(frameNr % 100 == 0)
        {
            Debug.Log($"{dscSize} {frameNr} {dscNr}");
        }
        
   
        mut.WaitOne();
       
        DecodedPointCloudData pcData;
        if (!inProgessFrames.TryGetValue(frameNr, out pcData))
        {
            pcData = new DecodedPointCloudData((int)frameNr, 125000, 3, new List<bool> { true, true, true })  ;
            inProgessFrames.Add(frameNr, pcData);
        }
        unsafe
        {
            Debug.Log($"Start decoding");
            IntPtr decoderPtr = DracoInvoker.decode_pc((byte*)rawDataPtr, dscSize);
            Debug.Log($"Decoding done");
            UInt32 nDecodedPoints = DracoInvoker.get_n_points(decoderPtr);
            Debug.Log($"Number of points after decoding {dscNr}: {nDecodedPoints}");
            IntPtr pointsPtr = DracoInvoker.get_point_array(decoderPtr);
            IntPtr colorPtr = DracoInvoker.get_color_array(decoderPtr);
            float* pointsUnsafePtr = (float*)pointsPtr;
            byte* colorsUnsafePtr = (byte*)colorPtr;
            int zeros = 0;
           // if(dscNr == 2)
           // {
                for (int i = 0; i < nDecodedPoints; i++)
                {
                    //    points[i] = new Vector3(0, 0, 0);
                    if (pointsUnsafePtr[(i * 3)] == 0 && pointsUnsafePtr[(i * 3) + 1] == 0 && pointsUnsafePtr[(i * 3) + 2] == 0)
                    {
                        zeros++;
                    }
                    pcData.Points.Add(new Vector3(pointsUnsafePtr[(i * 3)] * -1, pointsUnsafePtr[(i * 3) + 1] * -1, pointsUnsafePtr[(i * 3) + 2] * -1));
                    pcData.Colors.Add(new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255));
                }
            //}
            
            Debug.Log($"ZEROES {dscNr} {zeros}");
            DracoInvoker.free_decoder(decoderPtr);
            Debug.Log($"Decoders freed");
        }
          DracoInvoker.free_description(dsc);
          pcData.CurrentNDescriptions++;
          if (pcData.MaxDescriptions == pcData.CurrentNDescriptions)
          {
              inProgessFrames.Remove(frameNr);
              queue.Enqueue(pcData);
          }
        mut.ReleaseMutex();
    }
    [MonoPInvokeCallback(typeof(DracoInvoker.freePCCallback))]
    static void OnFreePCCallback(IntPtr pc)
    {
        Debug.Log("Free PC");
        Realsense2Invoker.free_point_cloud(pc);
    }
    public void OnEnable()
    {
        
    }


    // Start is called before the first frame update
    void Start()
    {
        Application.targetFrameRate = 120;
        var sessionInfo = SessionInfo.CreateFromJSON(Application.dataPath + "/config/session_config.json");
        Debug.Log(sessionInfo.sfuAddress + " " + sessionInfo.peerUDPPort);
        ClientID = sessionInfo.clientID;
        queue = new ConcurrentQueue<DecodedPointCloudData>();
        inProgessFrames = new();
        //meshFilter = GetComponent<MeshFilter>();
        Realsense2Invoker.RegisterDebugCallback(OnDebugCallback);
        Realsense2Invoker.set_logging("", debug);
        DracoInvoker.RegisterDebugCallback(OnDebugCallbackDraco);
        DracoInvoker.set_logging("", debug);
        int initCode = Realsense2Invoker.initialize(sessionInfo.camWidth, sessionInfo.camHeight, sessionInfo.camFPS, sessionInfo.camClose, sessionInfo.camFar, sessionInfo.useCam);
        DracoInvoker.register_description_done_callback(OnDescriptionDoneCallback);
        DracoInvoker.register_free_pc_callback(OnFreePCCallback);
        DracoInvoker.initialize();
        Debug.Log(initCode);
        if(initCode == 0)
        {
            for(int i = 0; i < renderers.Count; i++)
            {
                filters.Add(renderers[i].GetComponent<MeshFilter>());
                int offset = i == ClientID ? 1 : 0;
                renderers[i].transform.position = new Vector3(sessionInfo.startPositions[i].x, sessionInfo.startPositions[i].y-offset, sessionInfo.startPositions[i].z);
                if (i == ClientID)
                {
                    var pcSelf = Instantiate(VRCam, renderers[i].transform.position, renderers[i].transform.rotation);
                    pcSelf.transform.parent = renderers[i].transform;
                }
                
            }
            Table.transform.position = new Vector3(sessionInfo.table.position.x, sessionInfo.table.position.y, sessionInfo.table.position.z);
            Table.transform.localScale = new Vector3(sessionInfo.table.scale.x, sessionInfo.table.scale.y, sessionInfo.table.scale.z);
            myThread = new System.Threading.Thread(pollFrames);
            myThread.Start();
        } else
        {
            Debug.Log($"Something went wrong inting the Realsense2: {initCode}");
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        if(!queue.IsEmpty)
        {
            DecodedPointCloudData dec;
            bool succes = queue.TryDequeue(out dec);
            if(succes)
            {
                Debug.Log("Dequeue Successful!");
                Destroy(currentMesh);
                currentMesh = new Mesh();
                currentMesh.indexFormat = dec.NPoints > 65535 ?
                        IndexFormat.UInt32 : IndexFormat.UInt16;
                currentMesh.SetVertices(dec.Points);
                currentMesh.SetColors(dec.Colors);
                currentMesh.SetIndices(
                    Enumerable.Range(0, currentMesh.vertexCount).ToArray(),
                    MeshTopology.Points, 0
                );
                Debug.Log($"NVertex: {currentMesh.vertexCount}");
                Debug.Log($"Bounds: {currentMesh.bounds}");
                currentMesh.UploadMeshData(true);
                for(int i = 0; i < filters.Count; i++)
                {
                    if(i != ClientID)
                    {
                        filters[i].mesh = currentMesh;
                    }
                }
              
                

            }
        }
    }

    public void OnDestroy()
    {
        keep_working = false;
        myThread.Join();
        DracoInvoker.clean_up();
    }

    void pollFrames()
    {
      
        while(keep_working)
        {
            Debug.Log($"Poll next");
            IntPtr frame = Realsense2Invoker.poll_next_point_cloud();
            Debug.Log($"Poll done");
            if ( frame != IntPtr.Zero )
            {
                Debug.Log($"Get size");
                uint nPoints = Realsense2Invoker.get_point_cloud_size(frame);
                Debug.Log($"Number of points: {nPoints}");
                int returnCode = DracoInvoker.encode_pc(frame);
            } else
            {
                Debug.Log("No frame"); 
                keep_working = false;
            }
            
        }
        Realsense2Invoker.clean_up();

    }
}
