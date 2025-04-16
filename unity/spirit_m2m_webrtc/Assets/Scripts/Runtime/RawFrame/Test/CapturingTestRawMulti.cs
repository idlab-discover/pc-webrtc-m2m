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
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;

using Debug = UnityEngine.Debug;

public class CapturingTestMultiRaw : MonoBehaviour
{
    public List<GameObject> renderers = new List<GameObject>();
    private List<MeshFilter> filters = new List<MeshFilter>();
    public GameObject VRCam;
    public GameObject Table;
    public int ClientID = 0;
    System.Threading.Thread myThread;
 //   static Dictionary<UInt32, DecodedRawFrame> inProgessFrames;
    //static ConcurrentQueue<DecodedRawFrame> queue;

    static Dictionary<UInt32, DecodedRawFrame> inProgessFrames2;
    static ConcurrentQueue<DecodedRawFrame> queue2;

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
    private static IntPtr rawConverter = IntPtr.Zero;
    private static IntPtr colorDecoder = IntPtr.Zero;
    private static IntPtr depthDecoder = IntPtr.Zero;
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
    static void OnColorDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
        if (frameNr % 100 == 0)
        {
            Debug.Log("Color enc: " + frameNr + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
        }
        if(frameNr == 500)
        {
            byte[] buffer = new byte[size];

            // Copy the data from the unmanaged memory to the byte array
            Marshal.Copy(rawDataPtr, buffer, 0,(int) size);

            // Write the byte array to the file
            File.WriteAllBytes("test.jpg", buffer);
        }
        IntPtr decoded_color = RawInvoker.decode_color(colorDecoder, rawDataPtr, size, width, height);
        if (rawConverter != IntPtr.Zero)
        {
            mut.WaitOne();
            DecodedRawFrame pcData2;
            if (!inProgessFrames2.TryGetValue(frameNr, out pcData2))
            {
                pcData2 = new DecodedRawFrame((int)frameNr, (int)nPoints, timestamp);
                inProgessFrames2.Add(frameNr, pcData2);
            }
            pcData2.DecodedColor = decoded_color;
            pcData2.ColorsCompleted = true;
            if (pcData2.IsCompleted)
            {
                
                IntPtr buf_depth = RawInvoker.get_decoded_depth_data(pcData2.DecodedDepth);
                IntPtr buf_color = RawInvoker.get_decoded_color_data(pcData2.DecodedColor);
                GCHandle hDepth = GCHandle.Alloc(pcData2.Points, GCHandleType.Pinned);
                GCHandle hColor = GCHandle.Alloc(pcData2.Colors, GCHandleType.Pinned);
                try
                {
                    Realsense2Invoker.convert_raw_frame(rawConverter, buf_depth, buf_color, hDepth.AddrOfPinnedObject(), hColor.AddrOfPinnedObject());
                }
                finally
                {
                    hDepth.Free();
                    hColor.Free();
                }

                pcData2.InitRawColors(width*height);
                unsafe
                {
                    byte* colorsUnsafePtr = (byte*)buf_color;
                    for (int i = 0; i < width * height; i++)
                    {
                        pcData2.DecodedColors[i] = new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255);
                    }
                }
               

                RawInvoker.free_decoded_color(pcData2.DecodedColor);
                RawInvoker.free_decoded_depth(pcData2.DecodedDepth);
                if (frameNr % 100 == 0)
                {
                    Debug.Log("Frame done2: " + frameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp));
                }
                inProgessFrames2.Remove(frameNr);
                queue2.Enqueue(pcData2);
            }
            mut.ReleaseMutex();
            return;
        }
  
    }

    [MonoPInvokeCallback(typeof(RawInvoker.depthDoneCallback))]
    static void OnDepthDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
        // Debug.Log("depth done: " + size + " " + width + " " + height);
        if (frameNr % 100 == 0)
        {
            Debug.Log("Depth enc: " + frameNr + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
        }
        IntPtr decoded_depth = RawInvoker.decode_depth(depthDecoder, rawDataPtr, width, height);
        if(rawConverter != IntPtr.Zero)
        {
            mut.WaitOne();
            DecodedRawFrame pcData2;
            if (!inProgessFrames2.TryGetValue(frameNr, out pcData2))
            {
                pcData2 = new DecodedRawFrame((int)frameNr, (int)nPoints, timestamp);
                inProgessFrames2.Add(frameNr, pcData2);
            }
            pcData2.DecodedDepth = decoded_depth;
            pcData2.PointsCompleted = true;
            if (pcData2.IsCompleted)
            {
                
                IntPtr buf_depth = RawInvoker.get_decoded_depth_data(pcData2.DecodedDepth);
                IntPtr buf_color = RawInvoker.get_decoded_color_data(pcData2.DecodedColor);
                GCHandle hDepth = GCHandle.Alloc(pcData2.Points, GCHandleType.Pinned);
                GCHandle hColor = GCHandle.Alloc(pcData2.Colors, GCHandleType.Pinned);
                try
                {
                    Realsense2Invoker.convert_raw_frame(rawConverter, buf_depth, buf_color, hDepth.AddrOfPinnedObject(), hColor.AddrOfPinnedObject());
                } finally
                {
                    hDepth.Free();
                    hColor.Free();
                }
                RawInvoker.free_decoded_color(pcData2.DecodedColor);
                RawInvoker.free_decoded_depth(pcData2.DecodedDepth);
                if (frameNr % 100 == 0)
                {
                    Debug.Log("Frame done2: " + frameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp));
                }
                inProgessFrames2.Remove(frameNr);
                queue2.Enqueue(pcData2);
            }
            mut.ReleaseMutex();
            return;
        }
        
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
        frameMode = FrameMode.RawData;
        sessionInfo.frameMode = FrameMode.RawData;
      //  sessionInfo.frameCodec = FrameCodec.Raw;
     //   queue = new ConcurrentQueue<DecodedRawFrame>();
      //  inProgessFrames = new();
        queue2 = new ConcurrentQueue<DecodedRawFrame>();
        inProgessFrames2 = new();
        //meshFilter = GetComponent<MeshFilter>();
        Realsense2Invoker.RegisterDebugCallback(OnDebugCallback);
        Realsense2Invoker.set_logging("", debug);
        RawInvoker.RegisterDebugCallback(OnDebugCallbackDraco);
        RawInvoker.set_logging("", debug);
        int initCode = Realsense2Invoker.initialize
        (
            sessionInfo.camWidth, sessionInfo.camHeight, sessionInfo.artificialSize, sessionInfo.camFPS, 
            sessionInfo.camClose, sessionInfo.camFar, sessionInfo.useCam, sessionInfo.frameMode,
            new FrameCleanupSettingsEx{
                blackoutBlockSize=sessionInfo.frameCleanupSettings.blackoutBlockSize, 
                shouldApplyDepthFilter=sessionInfo.frameCleanupSettings.shouldApplyDepthFilter,
                shouldBlackout=sessionInfo.frameCleanupSettings.shouldBlackout,
                shouldCleanupDepth = sessionInfo.frameCleanupSettings.shouldCleanupDepth
            }    
        );
        RawInvoker.register_depth_done_callback(OnDepthDoneCallback);
        RawInvoker.register_color_done_callback(OnColorDoneCallback);
        RawInvoker.register_free_frame_callback(OnFreeFrameCallback);
        
        if(sessionInfo.useCam)
        {
            tex = new Texture2D((int)sessionInfo.camWidth, (int)sessionInfo.camHeight);
            RawImg.rectTransform.sizeDelta = new Vector2(sessionInfo.camWidth, (int)sessionInfo.camHeight);
            RawImg.rectTransform.localScale = new Vector2(0.5f, 0.5f);
            CapturerIntrinsics depthInt = Realsense2Invoker.get_depth_intrinsics();
            CapturerIntrinsics colorInt = Realsense2Invoker.get_color_intrinsics();
            Debug.Log("D INT: " + depthInt.ToString());
            Debug.Log("C INT: " + colorInt.ToString());
            var cCodec = ColorCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            var dCodec = DepthCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            colorDecoder = RawInvoker.create_color_decoder(cCodec.CodecType);
            depthDecoder = RawInvoker.create_depth_decoder(dCodec.CodecType);
            RawInvoker.initialize(sessionInfo.camWidth, sessionInfo.camHeight, 
                cCodec.CodecType, cCodec.SettingsPtr, dCodec.CodecType, dCodec.SettingsPtr
            );
            rawConverter = Realsense2Invoker.create_new_raw_converter(true, depthInt, colorInt);
        } else
        {
            tex = new Texture2D((int)sessionInfo.artificialSize * (int)sessionInfo.artificialSize, (int)sessionInfo.artificialSize);
         //   tex.filterMode = FilterMode.Point;
           tex.wrapMode =TextureWrapMode.Clamp;
            var cCodec = ColorCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            var dCodec = DepthCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            RawInvoker.initialize(sessionInfo.artificialSize* sessionInfo.artificialSize, sessionInfo.artificialSize, 
                cCodec.CodecType, cCodec.SettingsPtr,
                dCodec.CodecType, dCodec.SettingsPtr
            );
           // RawImg.rectTransform.sizeDelta = new Vector2(75, 75);
            RawImg.rectTransform.localScale = new Vector2(2f, 2f);
            CapturerIntrinsics depthInt = Realsense2Invoker.get_depth_intrinsics();
            CapturerIntrinsics colorInt = Realsense2Invoker.get_color_intrinsics();
            Debug.Log("INT: "+ depthInt.ToString());
            rawConverter = Realsense2Invoker.create_new_raw_converter(false, depthInt, colorInt);
            colorDecoder = RawInvoker.create_color_decoder(cCodec.CodecType);
            depthDecoder = RawInvoker.create_depth_decoder(dCodec.CodecType);
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
                    if(sessionInfo.useCam)
                    {
                        renderers[i].transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                      //  renderers[i].transform.rotation = 
                    } else
                    {
                        renderers[i].transform.localScale = new Vector3(1.00f, 1.0f, 1.0f);
                    }
                   
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
        
        if (!queue2.IsEmpty)
        {
            bool succes = queue2.TryDequeue(out var c);
            if (succes)
            {
                Debug.Log("Dequeue Successful!");
                tex.SetPixels32(c.DecodedColors);
                tex.Apply();
                   Destroy(currentMesh);
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
        if(colorDecoder  != IntPtr.Zero)
        {
            RawInvoker.free_color_decoder(colorDecoder); 
            colorDecoder = IntPtr.Zero;
        }
        if (depthDecoder != IntPtr.Zero)
        {
            RawInvoker.free_depth_decoder(depthDecoder);
            depthDecoder = IntPtr.Zero;
        }
    }
}
