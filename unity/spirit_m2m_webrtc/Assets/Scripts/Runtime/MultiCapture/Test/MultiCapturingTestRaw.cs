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
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public class MultiCapturingTestRaw : MonoBehaviour
{
    const string NAME = "MultiCapturingTestRaw";

    private MultiCaptureSetup capture;
    public List<GameObject> renderers = new List<GameObject>();
    private List<MeshFilter> filters = new List<MeshFilter>();
    public GameObject VRCam;
    public GameObject Table;
    public int ClientID = 0;
    System.Threading.Thread myThread;
    static Dictionary<UInt32, DecodedRawFrameMulti> inProgessFrames;
    static ConcurrentQueue<DecodedRawFrameMulti> queue;
    private static Mutex mut = new Mutex();
    Mesh currentMesh;
    //private MeshFilter meshFilter;
    public int debug = 0;
    private FrameMode frameMode;
    private RawEncodingQueue rawEncodingQueue;
    private static List<IntPtr> rawConverters;
    private static List<IntPtr> colorDecoders;
    private static List<IntPtr> depthDecoders;
    private static uint numberOfCaptures;
    static bool keep_working = true;
    static UInt64 tsLastRendered = 0;
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

    [MonoPInvokeCallback(typeof(RawInvoker.colorDoneCallback))]
    static void OnColorDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 capturerID, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
          if (frameNr % 100 == 0)
          {
                Debug.Log("Color enc: " + capturerID + " " + frameNr + " " + width + " " + height + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
          }
        IntPtr decoded_color = RawInvoker.decode_color(colorDecoders[(int)capturerID], rawDataPtr, size, width, height);
        IntPtr rawConverter = rawConverters[(int)capturerID];
        if (rawConverter != IntPtr.Zero)
        {
            mut.WaitOne();
            DecodedRawFrameMulti pcData2;
            if (!inProgessFrames.TryGetValue(frameNr, out pcData2))
            {
                pcData2 = new DecodedRawFrameMulti(2, frameNr, timestamp);
                inProgessFrames.Add(frameNr, pcData2);
            }
            DecodedRawFrameSingle s = pcData2.GetSingle(capturerID) ?? pcData2.AddSingle(capturerID, nPoints);
            s.DecodedColor = decoded_color;
            s.ColorsCompleted = true;
            if (s.IsCompleted)
            {

                IntPtr buf_depth = RawInvoker.get_decoded_depth_data(s.DecodedDepth);
                IntPtr buf_color = RawInvoker.get_decoded_color_data(s.DecodedColor);
                GCHandle hDepth = GCHandle.Alloc(pcData2.Points, GCHandleType.Pinned);
                GCHandle hColor = GCHandle.Alloc(pcData2.Colors, GCHandleType.Pinned);
                try
                {
                        // TODO
                    Realsense2Invoker.convert_raw_frame(rawConverter, buf_depth, buf_color, hDepth.AddrOfPinnedObject()+(int)(s.PointOffset*3*sizeof(float)), hColor.AddrOfPinnedObject()+(int)(s.PointOffset * 4));
                }
                finally
                {
                    hDepth.Free();
                    hColor.Free();
                }

                s.InitRawColors(width * height);
                unsafe
                {
                    byte* colorsUnsafePtr = (byte*)buf_color;
                    for (int i = 0; i < width * height; i++)
                    {
                        s.DecodedColors[i] = new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255);
                    }
                }


                RawInvoker.free_decoded_color(s.DecodedColor);
                RawInvoker.free_decoded_depth(s.DecodedDepth);
                if (frameNr % 100 == 0)
                {
                    Debug.Log("Frame done2: " + frameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp));
                }
                if(pcData2.IsCompleted)
                {
                    inProgessFrames.Remove(frameNr);
                    queue.Enqueue(pcData2);
                }
                
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
            Debug.Log("Depth enc: " + capturerID +  " " + frameNr + " " + size + " " + (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)timestamp));
        }

        IntPtr decoded_depth = RawInvoker.decode_depth(depthDecoders[(int)capturerID], rawDataPtr, width, height);
        IntPtr rawConverter = rawConverters[(int)capturerID];
        if (rawConverter != IntPtr.Zero)
        {
            mut.WaitOne();
            DecodedRawFrameMulti pcData2;
            if (!inProgessFrames.TryGetValue(frameNr, out pcData2))
            {
                pcData2 = new DecodedRawFrameMulti(2, frameNr, timestamp);
                inProgessFrames.Add(frameNr, pcData2);
            }
            DecodedRawFrameSingle s = pcData2.GetSingle(capturerID) ?? pcData2.AddSingle(capturerID, nPoints);
            s.DecodedDepth = decoded_depth;
            s.PointsCompleted = true;
            if (s.IsCompleted)
            {

                IntPtr buf_depth = RawInvoker.get_decoded_depth_data(s.DecodedDepth);
                IntPtr buf_color = RawInvoker.get_decoded_color_data(s.DecodedColor);
                GCHandle hDepth = GCHandle.Alloc(pcData2.Points, GCHandleType.Pinned);
                GCHandle hColor = GCHandle.Alloc(pcData2.Colors, GCHandleType.Pinned);
                try
                {
                    // TODO
                    Realsense2Invoker.convert_raw_frame(rawConverter, buf_depth, buf_color, hDepth.AddrOfPinnedObject() + (int)(s.PointOffset * 3 * sizeof(float)), hColor.AddrOfPinnedObject() + (int)(s.PointOffset * 4));
                }
                finally
                {
                    hDepth.Free();
                    hColor.Free();
                }

                s.InitRawColors(width * height);
                unsafe
                {
                    byte* colorsUnsafePtr = (byte*)buf_color;
                    for (int i = 0; i < width * height; i++)
                    {
                        s.DecodedColors[i] = new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255);
                    }
                }


                RawInvoker.free_decoded_color(s.DecodedColor);
                RawInvoker.free_decoded_depth(s.DecodedDepth);
                if (frameNr % 100 == 0)
                {
                    Debug.Log("Frame done2: " + frameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - timestamp));
                }
                if (pcData2.IsCompleted)
                {
                    inProgessFrames.Remove(frameNr);
                    queue.Enqueue(pcData2);
                }

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
    }
    void OnFrameReady(uint capturerID, IntPtr framePtr, bool isFrameValid)
    {
        Debug.Log("Frame is ready" + capturerID + " VALID: " + isFrameValid);
        if(framePtr == IntPtr.Zero)
        {
            return;
        }
        rawEncodingQueue.EncodeFrame(framePtr);
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
        Logger.Init(sessionInfo.loggerSettings);
        RawInvoker.RegisterLogToFileCallback(DLLLogger.OnLogToFileCallback);
        Logger.Log($"id=MultiTestRaw ts={Logger.Time} status={Logger.Status.Creating}");
        Logger.ForceFlush();
        sessionInfo.frameMode = FrameMode.RawData;
        ClientID = sessionInfo.clientID;
        frameMode = sessionInfo.frameMode;
        queue = new ConcurrentQueue<DecodedRawFrameMulti>();
        inProgessFrames = new();
        //meshFilter = GetComponent<MeshFilter>();
        Realsense2Invoker.RegisterDebugCallback(OnDebugCallback);
        Realsense2Invoker.set_logging("", debug);
        RawInvoker.RegisterDebugCallback(OnDebugCallbackDraco);
        RawInvoker.set_logging("", debug);
        capture = CaptureFactory.CreateNewMultiCapture(sessionInfo);
      
        DracoInvoker.initialize();
       
        if(capture != null)
        {
            for (int i = 0; i < renderers.Count; i++)
            {
                filters.Add(renderers[i].GetComponent<MeshFilter>());
                int offset = i == ClientID ? 1 : 0;
                renderers[i].transform.position = new Vector3(sessionInfo.startPositions[i].x, sessionInfo.startPositions[i].y-offset, sessionInfo.startPositions[i].z);
                if (i == ClientID)
                {
                    float xPush = renderers[i].transform.position.x - sessionInfo.table.position.x;
                    float zPush = renderers[i].transform.position.z - sessionInfo.table.position.z;
                    Vector2 vPush = new Vector2(xPush, zPush);
                    renderers[i].transform.position = new Vector3(renderers[i].transform.position.x+vPush.normalized.x*0.5f, renderers[i].transform.position.y, renderers[i].transform.position.z + vPush.normalized.y*0.5f);
                    var pcSelf = Instantiate(VRCam, renderers[i].transform.position, renderers[i].transform.rotation);
              
                    pcSelf.transform.parent = renderers[i].transform;
                }
                
            }
            Table.transform.position = new Vector3(sessionInfo.table.position.x, sessionInfo.table.position.y, sessionInfo.table.position.z);
            Table.transform.localScale = new Vector3(sessionInfo.table.scale.x, sessionInfo.table.scale.y, sessionInfo.table.scale.z);
            rawConverters = new List<IntPtr>((int)capture.NumberOfCapturers);
            colorDecoders = new List<IntPtr>((int)capture.NumberOfCapturers);
            depthDecoders = new List<IntPtr>((int)capture.NumberOfCapturers);
          
            var cCodec = ColorCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            var dCodec = DepthCodecHelper.GetCodecSettings(sessionInfo.rawEncodingSettings);
            for (int i = 0; i < capture.NumberOfCapturers; i++)
            {
                rawConverters.Add(Realsense2Invoker.create_new_raw_converter(capture.CapType, capture.GetCalibrationForCapturer((uint)i)));
                colorDecoders.Add(RawInvoker.create_color_decoder(cCodec.CodecType));
                depthDecoders.Add(RawInvoker.create_depth_decoder(dCodec.CodecType));
            }
            Debug.Log("Number of Capturers: " + depthDecoders.Count);
            numberOfCaptures = capture.NumberOfCapturers;
            // TODO get width, height, n_capturers from capturing ptr
            rawEncodingQueue = new RawEncodingQueue(sessionInfo, 1280, 720, capture.NumberOfCapturers);
            rawEncodingQueue.SetColorDoneCallback(OnColorDoneCallback);
            rawEncodingQueue.SetDepthDoneCallback(OnDepthDoneCallback);
            rawEncodingQueue.SetFreeFrameCallback(OnFreeFrameCallback);
            capture.SetFrameReadyCallback(OnFrameReady);
            //  myThread = new System.Threading.Thread(pollFrames);
          //  myThread.Start();
        } else
        {
            Debug.Log($"Something went wrong inting the Realsense2");
        }
        
    }

    // Update is called once per frame
    void Update()
    {
        if(!queue.IsEmpty)
        {
            DecodedRawFrameMulti dec;
            bool succes = queue.TryDequeue(out dec);
            if(succes)
            {
                Debug.Log("Dequeue Successful!");
                Destroy(currentMesh);
                currentMesh = new Mesh();
                currentMesh.indexFormat = dec.TotalPoints > 65535 ?
                        IndexFormat.UInt32 : IndexFormat.UInt16;
                currentMesh.SetVertices(dec.Points);
                currentMesh.SetColors(dec.Colors);
                currentMesh.SetIndices(
                    Enumerable.Range(0, currentMesh.vertexCount).ToArray(),
                    MeshTopology.Points, 0
                );
               // Debug.Log($"NVertex: {currentMesh.vertexCount}");
               // Debug.Log($"Bounds: {currentMesh.bounds}");
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
        Debug.Log("DESTROYING");
     //   keep_working = false;
      //  myThread.Join();
        if(capture != null)
        {
            capture.Dispose();
        }
        if(rawEncodingQueue != null)
        {
            rawEncodingQueue.Dispose();
        }
        DracoInvoker.clean_up();
        Logger.ForceFlush();
    }

    void pollFrames()
    {
       
        while(keep_working)
        {
            switch(frameMode)
            {
                    case FrameMode.RawData:
                    {
                        Debug.Log($"Poll next pc");
                   
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
                   
            }
            
            
        }
        Realsense2Invoker.clean_up();
        if(capture != null)
        {
            capture.Dispose();
            capture = null;
        }

    }
}
