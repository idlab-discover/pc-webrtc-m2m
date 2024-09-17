using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

using UnityEngine;

public class PCSelf : MonoBehaviour
{
    public float CamClose;
    public float CamFar;
    public uint CamWidth;
    public uint CamHeight;
    public uint CamFPS;
    public bool UseCam;

    public Camera cam;
    public GameObject camOffset;
    public float CameraUpdateTimer = 0.300f;
    private float currentCameraUpdateTimer = 0;

    System.Threading.Thread workerThread;
    static bool keep_working = true;
    [MonoPInvokeCallback(typeof(DracoInvoker.descriptionDoneCallback))]
    static void OnDescriptionDoneCallback(IntPtr dsc, IntPtr rawDataPtr, UInt32 totalPointsInCloud, UInt32 dscSize, UInt32 frameNr, UInt32 dscNr)
    {
        if (keep_working)
        {
            byte[] frameHeader = new byte[8];
            var frameNrField = BitConverter.GetBytes(frameNr);
            frameNrField.CopyTo(frameHeader, 0);
            var nPointsFrameField = BitConverter.GetBytes(totalPointsInCloud);
            nPointsFrameField.CopyTo(frameHeader, 4);
            byte[] messageBuffer = new byte[frameHeader.Length + dscSize];
            System.Buffer.BlockCopy(frameHeader, 0, messageBuffer, 0, frameHeader.Length);
            Marshal.Copy(rawDataPtr, messageBuffer, frameHeader.Length, (int)dscSize);
            int nSend = 0;
            if(frameNr % 10 ==0)
            {
                Debug.Log($"{frameNr} {dscNr} {dscSize}");
            }
           
            unsafe
            {
                fixed (byte* bufferPointer = messageBuffer)
                {
                    nSend = WebRTCInvoker.send_tile(bufferPointer, (uint)messageBuffer.Length, dscNr);
                }
            }
           
            if (nSend == -1)
            {
               keep_working = false;
               Debug.Log("Stop capturing");
            }
        }
        DracoInvoker.free_description(dsc);

    }
    [MonoPInvokeCallback(typeof(DracoInvoker.freePCCallback))]
    static void OnFreePCCallback(IntPtr pc)
    {
        Realsense2Invoker.free_point_cloud(pc);
    }
    // Start is called before the first frame update
    void Start()
    {
        DracoInvoker.register_description_done_callback(OnDescriptionDoneCallback);
        DracoInvoker.register_free_pc_callback(OnFreePCCallback);
        DracoInvoker.initialize();
        int initCode = Realsense2Invoker.initialize(CamWidth, CamHeight, CamFPS, CamClose, CamFar, UseCam);
        if (initCode == 0)
        {
            workerThread = new System.Threading.Thread(pollFrames);
            workerThread.Start();
        }
        else
        {
            Debug.Log($"Something went wrong inting the Realsense2: {initCode}");
        }
    }

    // Update is called once per frame
    void Update()
    {
        currentCameraUpdateTimer += Time.deltaTime;
        float horizontalInput = Input.GetAxis("Horizontal");
        float verticalInput = Input.GetAxis("Vertical");

        camOffset.transform.eulerAngles = new Vector3(camOffset.transform.rotation.eulerAngles.x + (100 * verticalInput * Time.deltaTime), camOffset.transform.rotation.eulerAngles.y+ (100* horizontalInput  * Time.deltaTime), 0);
        if(currentCameraUpdateTimer > CameraUpdateTimer)
        {
            Matrix4x4 worldToCameraMatrix = cam.worldToCameraMatrix;
            Matrix4x4 projectionMatrix = cam.projectionMatrix;
            Debug.Log(worldToCameraMatrix);
            Debug.Log(projectionMatrix);
            string output = "";
            for (int i = 0; i < 4; i++)
            {
                Vector4 row = worldToCameraMatrix.GetRow(i);
                for (int j = 0; j < 4; j++)
                {
                    output += row[j].ToString("0.00000") + ";";
                }
            }
            for (int i = 0; i < 4; i++)
            {
                Vector4 row = projectionMatrix.GetRow(i);
                for (int j = 0; j < 4; j++)
                {
                    output += row[j].ToString("0.00000") + ";";
                }
            }

            Vector3 pos = transform.position;
            Debug.Log(pos);
            output += $"{pos.x};{pos.y+1};{pos.z};";
            byte[] outputBytes = Encoding.ASCII.GetBytes(output);
            unsafe
            {
                fixed (byte* bufferPointer = outputBytes)
                {
                    WebRTCInvoker.send_control_packet(bufferPointer, (uint)outputBytes.Length);
                }
            }
            currentCameraUpdateTimer -= CameraUpdateTimer;
        }
    }
    void OnDestroy()
    {
        keep_working = false;
        workerThread.Join();
        DracoInvoker.clean_up();
    }
    void pollFrames()
    {
        keep_working = true;
        WebRTCInvoker.wait_for_peer();
       

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
                if(returnCode == 0 )
                {
                    Debug.Log("Enqueue frame");
                }
            } else
            {
                Debug.Log("No frame"); 
                keep_working = false;
            }
            
        }
        Realsense2Invoker.clean_up();
    }

    void dataEncodedCallback()
    {

    }
}
