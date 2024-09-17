using AOT;
using Draco;
using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.Collections;
using Unity.Jobs;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;

public class CapturingTest : MonoBehaviour
{
    /*System.Threading.Thread myThread;
    ConcurrentQueue<DecodedPointCloudData> queue;

    Mesh currentMesh;
    private MeshFilter meshFilter;
    public int debug = 2;

    bool keep_working = true;
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
    public void OnEnable()
    {
        
    }


    // Start is called before the first frame update
    void Start()
    {
        queue = new ConcurrentQueue<DecodedPointCloudData>();
        meshFilter = GetComponent<MeshFilter>();
        Realsense2Invoker.RegisterDebugCallback(OnDebugCallback);
        Realsense2Invoker.set_logging("", debug);
        DracoInvoker.RegisterDebugCallback(OnDebugCallbackDraco);
        DracoInvoker.set_logging("", debug);
        int initCode = Realsense2Invoker.initialize(848, 480, 30, false);
        Debug.Log(initCode);
        if(initCode == 0)
        {
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
                currentMesh.UploadMeshData(true);
                meshFilter.mesh = currentMesh;
            }
        }
    }

    public void OnDestroy()
    {
        keep_working = false;
        myThread.Join();
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
                IntPtr encoderPtr = DracoInvoker.encode_pc(frame);
                Debug.Log($"Encoding done");
                Realsense2Invoker.free_point_cloud(frame);
                Debug.Log($"Frame freed");
                IntPtr rawDataPtr = DracoInvoker.get_raw_data(encoderPtr);
                Debug.Log($"Raw data got");
                UInt32 encodedSize = DracoInvoker.get_encoded_size(encoderPtr);

                Debug.Log($"Encoded size: {encodedSize}");
                unsafe
                {
                    Debug.Log($"Start decoding");
                    IntPtr decoderPtr = DracoInvoker.decode_pc((byte*)rawDataPtr, encodedSize);
                    Debug.Log($"Decoding done");
                    UInt32 nDecodedPoints = DracoInvoker.get_n_points(decoderPtr);
                    Debug.Log($"Number of points after decoding: {nDecodedPoints}");
                    IntPtr pointsPtr = DracoInvoker.get_point_array(decoderPtr);
                    IntPtr colorPtr = DracoInvoker.get_color_array(decoderPtr);
                    float* pointsUnsafePtr = (float*)pointsPtr;
                    byte* colorsUnsafePtr = (byte*)colorPtr;

                    List<Vector3> points = new((int)nDecodedPoints);
                    List<Color32> colors = new((int)nDecodedPoints);
                    for(int i = 0; i < nDecodedPoints; i++)
                    {
                    //    points[i] = new Vector3(0, 0, 0);
                        points.Add(new Vector3(pointsUnsafePtr[(i*3)], pointsUnsafePtr[(i*3)+1], pointsUnsafePtr[(i*3)+2]));
                        colors.Add(new Color32(colorsUnsafePtr[(i*3)], colorsUnsafePtr[(i*3) + 1], colorsUnsafePtr[(i*3) + 2], 255));
                    }
                    Debug.Log($"{colors[(int)nDecodedPoints-1].r} {colors[0].g} {colors[(int)nDecodedPoints - 1].b}");
                    queue.Enqueue(new DecodedPointCloudData(points, colors));
                    Debug.Log($"Free coders");
                    DracoInvoker.free_encoder(encoderPtr);
                    DracoInvoker.free_decoder(decoderPtr);
                    Debug.Log($"Coders freed");
                }


            } else
            {
                Debug.Log("No frame"); 
                keep_working = false;
            }
            
        }
        Realsense2Invoker.clean_up();
    }*/
}
