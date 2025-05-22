using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using UnityEngine;

public class PCSelf : MonoBehaviour
{
   // public float CamClose;
   // public float CamFar;
   // public uint CamWidth;
   // public uint CamHeight;
  //  public uint CamFPS;
  //  public bool UseCam;
  //  public FrameMode FrameMode;
    public SessionInfo SessionInfo;
    private SingleCapture capture;

    public Camera cam;
    public AudioCapture AudioCapturePrefab;
    public GameObject camOffset;
    public float CameraUpdateTimer = 0.300f;
    private float currentCameraUpdateTimer = 0;
    private AudioCapture audioCapture;
    private bool useMic;

    System.Threading.Thread workerThread;
    static bool keep_working = true;
    #region Draco Functions
    [MonoPInvokeCallback(typeof(DracoInvoker.descriptionDoneCallback))]
    static void OnDescriptionDoneCallback(IntPtr dsc, IntPtr rawDataPtr, UInt32 totalPointsInCloud, UInt32 dscSize, UInt32 frameNr, UInt32 dscNr, UInt64 timestamp)
    {
        if (keep_working)
        {
            byte[] frameHeader = new byte[20];
            var timestampField = BitConverter.GetBytes(timestamp);
            timestampField.CopyTo(frameHeader, 0);
            var frameNrField = BitConverter.GetBytes(frameNr);
            frameNrField.CopyTo(frameHeader, 8);
            var codecType = BitConverter.GetBytes((uint)FrameCodec.Draco);
            codecType.CopyTo(frameHeader, 12);
            var nPointsFrameField = BitConverter.GetBytes(totalPointsInCloud);
            nPointsFrameField.CopyTo(frameHeader, 16);
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
    #endregion

    #region Raw Functions
    static void SendRawData(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp, FrameType frameType)
    {
       // return;
        if (keep_working)
        {
            byte[] frameHeader = new byte[32];
            var timestampField = BitConverter.GetBytes(timestamp);
            timestampField.CopyTo(frameHeader, 0);
            var frameNrField = BitConverter.GetBytes(frameNr);
            frameNrField.CopyTo(frameHeader, 8);
            var codecType = BitConverter.GetBytes((uint)FrameCodec.Raw);
            codecType.CopyTo(frameHeader, 12);
           // var codecType = BitConverter.GetBytes((uint)FrameCodec.Raw);
           // codecType.CopyTo(frameHeader, 12);
            var nPointsFrameField = BitConverter.GetBytes(nPoints);
            nPointsFrameField.CopyTo(frameHeader, 20);
            var widthFrameField = BitConverter.GetBytes(width);
            widthFrameField.CopyTo(frameHeader, 24);
            var heightFrameField = BitConverter.GetBytes(height);
            heightFrameField.CopyTo(frameHeader, 28);
            var sizeFrameField = BitConverter.GetBytes(size);
            sizeFrameField.CopyTo(frameHeader, 32);
            byte[] messageBuffer = new byte[frameHeader.Length + size];
            System.Buffer.BlockCopy(frameHeader, 0, messageBuffer, 0, frameHeader.Length);
            Marshal.Copy(rawDataPtr, messageBuffer, frameHeader.Length, (int)size);
            int nSend = 0;

            unsafe
            {
                fixed (byte* bufferPointer = messageBuffer)
                {
                    nSend = WebRTCInvoker.send_tile(bufferPointer, (uint)messageBuffer.Length, (uint)frameType);
                }
            }

            if (nSend == -1)
            {
                keep_working = false;
                Debug.Log("Stop capturing");
            }
        }
    }
    [MonoPInvokeCallback(typeof(RawInvoker.colorDoneCallback))]
    static void OnColorDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
            SendRawData(rawDataPtr, size, frameNr, width, height, nPoints, timestamp, FrameType.ColorFrame);
    }

    [MonoPInvokeCallback(typeof(RawInvoker.depthDoneCallback))]
    static void OnDepthDoneCallback(IntPtr rawDataPtr, UInt32 size, UInt32 frameNr, UInt32 width, UInt32 height, UInt32 nPoints, UInt64 timestamp)
    {
            SendRawData(rawDataPtr, size, frameNr, width, height, nPoints, timestamp, FrameType.DepthFrame);
    }


    [MonoPInvokeCallback(typeof(RawInvoker.freeFrameCallback))]
    static void OnFreeFrameCallback(IntPtr f)
    {
        Realsense2Invoker.free_raw_frame(f);
    }
    #endregion
    // Start is called before the first frame update
    void Start()
    {
        if(SessionInfo.frameCodec == FrameCodec.Draco)
        {
            DracoInvoker.register_description_done_callback(OnDescriptionDoneCallback);
            DracoInvoker.register_free_pc_callback(OnFreePCCallback);
            DracoInvoker.initialize();
        } else
        {
            RawInvoker.register_color_done_callback(OnColorDoneCallback);
            RawInvoker.register_depth_done_callback(OnDepthDoneCallback);
            RawInvoker.register_free_frame_callback(OnFreeFrameCallback);
            var cCodec = ColorCodecHelper.GetCodecSettings(SessionInfo.rawEncodingSettings);
            var dCodec = DepthCodecHelper.GetCodecSettings(SessionInfo.rawEncodingSettings);
            if(SessionInfo.capturerName != "artificial")
            {

                RawInvoker.initialize(SessionInfo.realsenseSettings.width, SessionInfo.realsenseSettings.height, 
                    cCodec.CodecType, cCodec.SettingsPtr, dCodec.CodecType, dCodec.SettingsPtr
                );
            } else
            {
                RawInvoker.initialize(SessionInfo.artificialSettings.artificialSize* SessionInfo.artificialSettings.artificialSize, SessionInfo.artificialSettings.artificialSize, 
                    cCodec.CodecType, cCodec.SettingsPtr, dCodec.CodecType, dCodec.SettingsPtr
                );
            }
            if(cCodec != null)
            {
                cCodec.Dispose();
            }
            
        }
        capture = CaptureFactory.CreateNewSingleCapture(SessionInfo);
       
        if (capture != null)
        {

            workerThread = new System.Threading.Thread(pollFrames);
            workerThread.Start();
        }
        else
        {
            Debug.Log($"Something went wrong inting the Realsense2");
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

        // No need to check frame codec here as cleanup will only happen when init
        DracoInvoker.clean_up();
        RawInvoker.clean_up();
    }
    void pollFrames()
    {
        keep_working = true;
        WebRTCInvoker.wait_for_peer();
        if(SessionInfo.frameCodec != FrameCodec.Draco)
        {
            CapturerIntrinsics dInt = capture.GetDepthIntrinsics();
            CapturerIntrinsics cInt = capture.GetColorIntrinsics();
            byte[] b = new byte[CapturerIntrinsics.Size() * 2];
            dInt.ConvertToBuffer().CopyTo(b, 0);
            cInt.ConvertToBuffer().CopyTo(b, CapturerIntrinsics.Size());
            unsafe
            {
                fixed (byte* bufferPointer = b)
                {
                    Debug.Log("sending intrsincs");
                    WebRTCInvoker.send_intrisics_packet(bufferPointer, (uint)b.Length);
                }
            }
        }
        

        while (keep_working)
        {
            switch (SessionInfo.frameMode)
            {
                case FrameMode.RealData:
                    {
                        IntPtr frame = capture.PollNextPointCloud();
                        Debug.Log($"Poll done");
                        if (frame != IntPtr.Zero)
                        {
                            uint nPoints = Realsense2Invoker.get_point_cloud_size(frame);
                            Debug.Log($"Number of points: {nPoints}");
                            int returnCode = DracoInvoker.encode_pc(frame);
                        }
                        else
                        {
                            keep_working = false;
                        }
                        break;
                    }
                case FrameMode.RawData:
                    {
                        IntPtr frame = capture.PollNextRawFrame();
                        if (frame != IntPtr.Zero)
                        {
                            RawInvoker.encode_frame(frame);
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
        if (capture != null)
        {
            capture.Dispose();
        }
    }


    #region Audio Functions
    public void InitAudioCapture()
    {
        useMic = true;
        audioCapture = Instantiate(AudioCapturePrefab, this.transform.position, this.transform.rotation);
        audioCapture.CB = CopyDataToPlayback;
        audioCapture.Init(SessionInfo.audioPlayback.codecName, SessionInfo.audioPlayback.dspSize);;
    }
    void CopyDataToPlayback(byte[] encodedData)
    {
        unsafe
        {
            fixed (byte* bufferPointer = encodedData)
            {
                WebRTCInvoker.send_audio(bufferPointer, (uint)encodedData.Length);
            }
        }
    }
    #endregion
}
