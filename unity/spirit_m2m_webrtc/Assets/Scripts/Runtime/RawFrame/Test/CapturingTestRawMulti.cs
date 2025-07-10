
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
using System.Drawing;

public class CapturingTestMultiRaw : MonoBehaviour
{
    public List<GameObject> renderers = new List<GameObject>();
    private List<MeshFilter> filters = new List<MeshFilter>();
    private RawEncodingQueue rawEncodingQueue;
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
    private static DepthDecoder depthDecoder;
    private SingleCapture capture;
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

    [Conditional("C1")]
    public void LogTest()
    {
        Debug.Log("SHOULD NOT RUN");
    }
    [MonoPInvokeCallback(typeof(RawInvoker.colorDoneCallback))]
    static void OnColorDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 capturerID, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
      //  if (frameNr % 100 == 0)
      //  {
            Debug.Log("Color enc: " + frameNr + " " + width + " " + height + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
     //   }
        Debug.Log("Color enc: " + fr + " " + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
        if (frameNr == 100)
        {
            byte[] buffer = new byte[size];

            // Copy the data from the unmanaged memory to the byte array
            Marshal.Copy(rawDataPtr, buffer, 0,(int) size);

            // Write the byte array to the file
            File.WriteAllBytes("kinect.jpg", buffer);
        }

        Logger.LogPCFrameStatusLimited("Test", Logger.Status.StartDecodingColor, capturerID, frameNr);
        IntPtr decoded_color = RawInvoker.decode_color(colorDecoder, rawDataPtr, size, width, height);
        Logger.LogPCFrameStatusLimited("Test", Logger.Status.EndDecodingColor, capturerID, frameNr);
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
                    Logger.LogPCFrameStatusLimited("Test", Logger.Status.StartRawConversion, capturerID, frameNr);
                    Realsense2Invoker.convert_raw_frame(rawConverter, buf_depth, buf_color, hDepth.AddrOfPinnedObject(), hColor.AddrOfPinnedObject());
                    Logger.LogPCFrameStatusLimited("Test", Logger.Status.EndRawConversion, capturerID, frameNr);
                }
                finally
                {
                    hDepth.Free();
                    hColor.Free();
                }

                pcData2.InitRawColors(width*height);
                Logger.LogPCFrameStatusLimited("Test", Logger.Status.StartRawColorCopy, capturerID, frameNr);
                unsafe
                {
                    byte* src = (byte*)buf_color;
                    fixed (Color32* dst = pcData2.DecodedColors)
                    {
                        Color32* colorPtr = dst;
                        for (int i = 0; i < width * height; i++)
                        {
                            colorPtr->r = *src++;
                            colorPtr->g = *src++;
                            colorPtr->b = *src++;
                            colorPtr->a = 255;
                            colorPtr++;
                        }
                    }
                }
                Logger.LogPCFrameStatusLimited("Test", Logger.Status.EndRawColorCopy, capturerID, frameNr);


                RawInvoker.free_decoded_color(pcData2.DecodedColor);
                RawInvoker.free_decoded_depth(pcData2.DecodedDepth);
                if (frameNr % 100 == 0)
                {
                    Debug.Log("Frame done2: " + frameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp));
                }
                inProgessFrames2.Remove(frameNr);
                Logger.LogPCFrameStatusLimited("Test", Logger.Status.FrameCompleted, capturerID, frameNr);
                queue2.Enqueue(pcData2);
            }
            mut.ReleaseMutex();
            return;
        }
  
    }

    [MonoPInvokeCallback(typeof(RawInvoker.depthDoneCallback))]
    static void OnDepthDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 capturerID, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
        // Debug.Log("depth done: " + size + " " + width + " " + height);
        if (frameNr % 100 == 0)
        {
            Debug.Log("Depth enc: " + frameNr + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
        }
        Logger.LogPCFrameStatusLimited("Test", Logger.Status.StartDecodingDepth, capturerID, frameNr);
        IntPtr decoded_depth = depthDecoder.DecodeDepth(rawDataPtr, width, height);
        Logger.LogPCFrameStatusLimited("Test", Logger.Status.EndDecodingDepth, capturerID, frameNr);
        if (rawConverter != IntPtr.Zero)
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
        LogTest();
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
        Logger.Init(sessionInfo.loggerSettings);
        RawInvoker.RegisterLogToFileCallback(DLLLogger.OnLogToFileCallback);
        //meshFilter = GetComponent<MeshFilter>();
        Realsense2Invoker.RegisterDebugCallback(OnDebugCallback);
        Realsense2Invoker.set_logging("", debug);
        RawInvoker.RegisterDebugCallback(OnDebugCallbackDraco);
        RawInvoker.set_logging("", debug);
        capture = CaptureFactory.CreateNewSingleCapture(sessionInfo);
        
        
        if(sessionInfo.capturerName != "artificial")
        {
            // TODO Maybe change this to physical camera width/height
            tex = new Texture2D((int)1280, (int)720);
            RawImg.rectTransform.sizeDelta = new Vector2(1280/4, 720/4);
        //    RawImg.rectTransform.localScale = new Vector2(0.5f, 0.5f);
            IntPtr cal = capture.GetCalibration();
            var cCodec = ColorCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            var dCodec = DepthCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            colorDecoder = RawInvoker.create_color_decoder(cCodec.CodecType);
            depthDecoder = new DepthDecoder(dCodec.CodecType, 0);
            rawEncodingQueue = new RawEncodingQueue(sessionInfo, 1280, 720, 1);
            rawEncodingQueue.SetColorDoneCallback(OnColorDoneCallback);
            rawEncodingQueue.SetDepthDoneCallback(OnDepthDoneCallback);
            rawEncodingQueue.SetFreeFrameCallback(OnFreeFrameCallback);
            rawConverter = Realsense2Invoker.create_new_raw_converter(capture.CaptureType, cal);

        } else
        {
            ArtificialCapture cap = (ArtificialCapture)capture;
            uint sideSize = cap.GetCalibrationStruct().sideSize;
            tex = new Texture2D((int)sideSize * (int)sideSize, (int)sideSize);
         //   tex.filterMode = FilterMode.Point;
           tex.wrapMode =TextureWrapMode.Clamp;
            var cCodec = ColorCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            var dCodec = DepthCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
          
            rawEncodingQueue = new RawEncodingQueue(sessionInfo, sideSize * sideSize, sideSize, 1);
            rawEncodingQueue.SetColorDoneCallback(OnColorDoneCallback);
            rawEncodingQueue.SetDepthDoneCallback(OnDepthDoneCallback);
            rawEncodingQueue.SetFreeFrameCallback(OnFreeFrameCallback);
            // RawImg.rectTransform.sizeDelta = new Vector2(75, 75);
            RawImg.rectTransform.localScale = new Vector2(2f, 2f);
            IntPtr cal = capture.GetCalibration();
            rawConverter = Realsense2Invoker.create_new_raw_converter(capture.CaptureType, cal);
            colorDecoder = RawInvoker.create_color_decoder(cCodec.CodecType);
            depthDecoder = new DepthDecoder(dCodec.CodecType, 0);
        }
        RawImg.texture = tex;
        
       
        if(capture != null)
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
                    if(sessionInfo.capturerName != "artificial")
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
            Debug.Log($"Something went wrong inting the Realsense2: ");
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
                Logger.LogPCFrameStatusLimited("Test", Logger.Status.FrameRendered, 0, (uint)c.FrameNr);

            }
        }
    }

    public void OnDestroy()
    {
        keep_working = false;
        myThread.Join();
        RawInvoker.clean_up();
    }
    static int fr = 0;
    void pollFrames()
    {
      
        while(keep_working)
        {
            switch(frameMode)
            {
                    case FrameMode.RealData:
                    {
                        Debug.Log($"Poll next");
                            fr++;
                        IntPtr frame = capture.PollNextPointCloud();
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
                      
                        IntPtr frame = capture.PollNextRawFrame();
                        fr++;
                        Debug.Log("Color enc: " + fr + " "  + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
                        Debug.Log($"Poll done");
                        if (frame != IntPtr.Zero)
                        {
                            Debug.Log($"Get size");
               //            if (fr % 20 == 0)
                             rawEncodingQueue.EncodeRawFrame(frame);
                             
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
        if(capture != null)
        {
            capture.Dispose();
        }

        if (rawEncodingQueue != null)
        {
            rawEncodingQueue.Dispose();
        }

        if (rawConverter != IntPtr.Zero)
        {
            Realsense2Invoker.free_raw_converter(rawConverter);
            rawConverter = IntPtr.Zero;   
        }
        if(colorDecoder  != IntPtr.Zero)
        {
            RawInvoker.free_color_decoder(colorDecoder); 
            colorDecoder = IntPtr.Zero;
        }
        if (depthDecoder != null)
        {
            depthDecoder.Dispose();
        }
        Logger.ForceFlush();
    }
}
