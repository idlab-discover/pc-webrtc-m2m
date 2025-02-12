using AOT;
//using Draco;
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
using UnityEditor.Search;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.UIElements;
using Debug = UnityEngine.Debug;

public class CapturingTestMultiRaw : MonoBehaviour
{
    public List<GameObject> renderers = new List<GameObject>();
    private List<MeshFilter> filters = new List<MeshFilter>();
    public GameObject VRCam;
    public GameObject Table;
    public int ClientID = 0;
    System.Threading.Thread myThread;
    static Dictionary<UInt32, DecodedRawFrame> inProgessFrames;
    static ConcurrentQueue<DecodedRawFrame> queue;
    private static Mutex mut = new Mutex();
    Mesh currentMesh;
    //private MeshFilter meshFilter;
    public int debug = 0;
    private FrameMode frameMode;

    static bool keep_working = true;
    public RawImage RawImg;
    private Texture2D tex;
   /// <summary>
   /// private static UnityEngine.Color[] c;
   /// </summary>
    private static bool colUpdate;
    private IntPtr rawConverter = IntPtr.Zero;
    enum Color { red, green, blue, black, white, yellow, orange };
    [MonoPInvokeCallback(typeof(DLLLogger.debugCallback))]
    static void OnDebugCallback(IntPtr request, int color, int size)
    {
        // Ptr to string
        string debug_string = Marshal.PtrToStringAnsi(request, size);
        // Add specified color
        debug_string =
            String.Format("Raw Capturing: {0}{1}{2}{3}{4}",
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

    [MonoPInvokeCallback(typeof(RawInvoker.colorDoneCallback))]
    static void OnColorDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt64 timestamp)
    {
        if (frameNr % 100 == 0)
        {
            Debug.Log("Color enc: " + frameNr + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
        }
        
        IntPtr decoded_color = RawInvoker.decode_color(rawDataPtr, size, width, height);
        IntPtr buf = RawInvoker.get_decoded_color_data(decoded_color);
        
        mut.WaitOne();
        DecodedRawFrame pcData;
        if (!inProgessFrames.TryGetValue(frameNr, out pcData))
        {
            pcData = new DecodedRawFrame((int)frameNr, (int)(width*height), timestamp);
            inProgessFrames.Add(frameNr, pcData);
        }
        unsafe
        {
            byte* colorsUnsafePtr = (byte*)buf;
            int zeros = 0;

            for (int i = 0; i < width * height; i++)
            {
                if (pcData.PointStatus[i])
                {
                    byte r = colorsUnsafePtr[(i * 3)];
                    byte g = colorsUnsafePtr[(i * 3) + 1];
                    byte b = colorsUnsafePtr[(i * 3) + 2];
                    pcData.Colors.Add(new Color32(r, g, b, 255));
                } else
                {
                    pcData.Colors.Add(new Color32(0, 0, 0, 0));
                }
                
            }
        }
        RawInvoker.free_decoded_color(decoded_color);
        pcData.ColorsCompleted = true;
        if (pcData.IsCompleted)
        {
            if(frameNr % 100 == 0)
            {
                Debug.Log("Frame done: " + frameNr + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
            }
            inProgessFrames.Remove(frameNr);
            queue.Enqueue(pcData);
        }
        mut.ReleaseMutex();
    }

    [MonoPInvokeCallback(typeof(RawInvoker.depthDoneCallback))]
    static void OnDepthDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt64 timestamp)
    {
        // Debug.Log("depth done: " + size + " " + width + " " + height);
       
        IntPtr decoded_depth = RawInvoker.decode_depth(rawDataPtr, width, height);
        IntPtr buf = RawInvoker.get_decoded_depth_data(decoded_depth);
        
  
        mut.WaitOne();
        DecodedRawFrame pcData;
        if (!inProgessFrames.TryGetValue(frameNr, out pcData))
        {
            pcData = new DecodedRawFrame((int)frameNr, (int)(width * height), timestamp);
            inProgessFrames.Add(frameNr, pcData);
        }
        unsafe
        {
            ushort* depthUnsafePtr = (ushort*)buf;
            if (buf == null || decoded_depth == null)
            {
                Debug.Log("depth null");
            }
            uint nP = 0;
            uint nActualP = 0;
            for (int i = 0; i < width; i++)
            {
                for(int j=0; j < height; j++)
                {
                    ushort t = depthUnsafePtr[nP];
                    if (t != 0)
                    {
                        pcData.Points.Add(new Vector3(i, j, t*0.001f));
                        pcData.PointStatus[nP] = true;
                    }
                    nP++;
                }
            }
            if (frameNr % 100 == 0)
            {
                Debug.Log("NPoints: " + frameNr + " " + pcData.Points.Count);
            }
        }
        
        RawInvoker.free_decoded_depth(decoded_depth);
        pcData.PointsCompleted = true;
        if (pcData.IsCompleted)
        {
            if (frameNr % 100 == 0)
            {
                Debug.Log("Frame done: " + frameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp));
            }
            inProgessFrames.Remove(frameNr);
            queue.Enqueue(pcData);
        }
        mut.ReleaseMutex();
    }

 
    [MonoPInvokeCallback(typeof(RawInvoker.freeFrameCallback))]
    static void OnFreeFrameCallback(IntPtr f)
    {
        Debug.Log("Free Raw Frame");
        Realsense2Invoker.free_raw_frame(f);
     //   Realsense2Invoker.free_point_cloud(pc); TODO
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
        frameMode = sessionInfo.frameMode;
        queue = new ConcurrentQueue<DecodedRawFrame>();
        inProgessFrames = new();
        //meshFilter = GetComponent<MeshFilter>();
        Realsense2Invoker.RegisterDebugCallback(OnDebugCallback);
        Realsense2Invoker.set_logging("", debug);
        RawInvoker.RegisterDebugCallback(OnDebugCallbackDraco);
        RawInvoker.set_logging("", debug);
        int initCode = Realsense2Invoker.initialize(sessionInfo.camWidth, sessionInfo.camHeight, sessionInfo.camFPS, sessionInfo.camClose, sessionInfo.camFar, sessionInfo.useCam, sessionInfo.frameMode);
        RawInvoker.register_depth_done_callback(OnDepthDoneCallback);
        RawInvoker.register_color_done_callback(OnColorDoneCallback);
        RawInvoker.register_free_frame_callback(OnFreeFrameCallback);
        if(sessionInfo.useCam)
        {
            tex = new Texture2D((int)sessionInfo.camWidth, (int)sessionInfo.camHeight);
            RawImg.rectTransform.sizeDelta = new Vector2(sessionInfo.camWidth, (int)sessionInfo.camHeight);
            RawImg.rectTransform.localScale = new Vector2(0.5f, 0.5f);
            RawInvoker.initialize(sessionInfo.camWidth, sessionInfo.camHeight, sessionInfo.jpegQuality);
        } else
        {
            tex = new Texture2D((int)75*75, (int)75);
            //tex.filterMode = FilterMode.Point;
            tex.wrapMode =TextureWrapMode.Clamp;
            RawInvoker.initialize(75*75, 75, sessionInfo.jpegQuality);
        }
        RawImg.texture = tex;
        
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
                } else
                {
                    renderers[i].transform.localScale = new Vector3(0.01f, 0.01f, 0.01f);
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
        if (!queue.IsEmpty)
        {
            bool succes = queue.TryDequeue(out var c);
            if (succes)
            {
                Debug.Log("Dequeue Successful!");
                tex.SetPixels32(c.Colors.ToArray());
                tex.Apply();
            /*   Destroy(currentMesh);
                currentMesh = new Mesh();
                currentMesh.indexFormat = c.NPoints > 65535 ?
                        IndexFormat.UInt32 : IndexFormat.UInt16;
                currentMesh.SetVertices(c.Points);
                currentMesh.SetColors(c.Colors);
                currentMesh.SetIndices(
                    Enumerable.Range(0, currentMesh.vertexCount).ToArray(),
                    MeshTopology.Points, 0
                );
                Debug.Log($"NVertex: {currentMesh.vertexCount}");
                Debug.Log($"Bounds: {currentMesh.bounds}");
                currentMesh.UploadMeshData(true);
                for (int i = 0; i < filters.Count; i++)
                {
                    if (i != ClientID)
                    {
                        filters[i].mesh = currentMesh;
                    }
                }
            */
            }

        }
    }

    public void OnDestroy()
    {
        keep_working = false;
        myThread.Join();
        RawInvoker.clean_up();
    }
    
    void pollFrames()
    {
      
        while(keep_working)
        {
            switch(frameMode)
            {
                    case FrameMode.RealData:
                    {
                        Debug.Log($"Poll next");
                        IntPtr frame = Realsense2Invoker.poll_next_raw_frame();
                        Debug.Log($"Poll done");
                        if (frame != IntPtr.Zero)
                        {
                            Debug.Log($"Get size");
                            uint nPoints = Realsense2Invoker.get_point_cloud_size(frame);
                            Debug.Log($"Number of points: {nPoints}");
                            int returnCode = DracoInvoker.encode_pc(frame);
                        }
                        else
                        {
                            Debug.Log("No frame");
                            keep_working = false;
                        }
                        break;
                    }
                    case FrameMode.RawData:
                    {
                        Debug.Log($"Poll next");
                        IntPtr frame = Realsense2Invoker.poll_next_raw_frame();
                        Debug.Log($"Poll done");
                        if (frame != IntPtr.Zero)
                        {
                            Debug.Log($"Get size");
               //             if (fr % 20 == 0)
                                RawInvoker.encode_frame(frame);
                          
                            
                            //  Debug.Log($"Number of points: {nPoints}");
                            // int returnCode = RawInvoker.encode_frame(frame);
                        }
                        else
                        {
                            Debug.Log("No frame");
                            keep_working = false;
                        }
                        break;
                    }
            }
            
            
        }
        Realsense2Invoker.clean_up();
        if(rawConverter != IntPtr.Zero)
        {
            Realsense2Invoker.free_raw_converter(rawConverter);
            rawConverter = IntPtr.Zero;
        }
    }
}
