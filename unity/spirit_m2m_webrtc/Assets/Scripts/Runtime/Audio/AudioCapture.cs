
using System;
using FMODUnity;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using FMOD;
using Unity.VisualScripting;
using Adrenak.UnityOpus;

public class AudioCapture : MonoBehaviour
{

    //public variables
    [Header("Capture Device details")]
    public int captureDeviceIndex = 0;
    [TextArea] public string captureDeviceName = null;

    FMOD.CREATESOUNDEXINFO exinfo;

    // Custom DSPCallback variables 

    private FMOD.DSP_READ_CALLBACK mReadCallback;
    private FMOD.DSP_READ_CALLBACK mPlaybackCallback;
    private FMOD.DSP mCaptureDSP;
    private FMOD.DSP mPlaybackDSP;
    public float[] mDataBuffer;
    private GCHandle mObjHandle;
    private GCHandle mObjHandle1;
    private uint mBufferLength;
    private uint soundLength;
    public int CaptureSrate;
    const int DRIFT_MS = 1;
    const int LATENCY_MS = 50;
    uint driftThreshold;
    uint desiredLatency;
    uint adjustedLatency;
    uint actualLatency;
    uint lastRecordPos = 0;
    uint samplesRecorded = 0;
    uint minRecordDelta = (uint)uint.MaxValue;


    public bool RecordingStarted = false;
    public delegate void CopyData(byte[] encodedData);
    public CopyData CB;

    FMOD.ChannelGroup masterCG;
    FMOD.ChannelGroup captureCG;
    FMOD.Channel channel;
    FMOD.Sound sound;

    private UInt32 frameNr;
    private AudioEncoder encoder;

    [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
    static FMOD.RESULT CaptureDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));
        
        IntPtr userData;
        functions.getuserdata(ref dsp_state, out userData);

        GCHandle objHandle = GCHandle.FromIntPtr(userData);
        AudioCapture obj = objHandle.Target as AudioCapture;

        Debug.Log("inchannels:" + inchannels);
        Debug.Log("outchannels:" + outchannels);
        Debug.Log("Length: " + length);
        Debug.Log("LengthOut: ");
        // Copy the incoming buffer to process later
        int lengthElements = (int)length * inchannels;
        // TODO can probably optimize this
        if(inchannels == 2 )
        {
            Marshal.Copy(inbuffer, obj.mDataBuffer, 0, (int)lengthElements);
            ulong timestamp = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            byte[] encodedData = obj.encoder.EncodeSample(new AudioFrame(obj.frameNr, timestamp, obj.mDataBuffer));
            obj.CB?.Invoke(encodedData);
            obj.frameNr++;
        }
        
        // Copy the inbuffer to the outbuffer so we can still hear it
        Array.Clear(obj.mDataBuffer, 0, lengthElements);
        Marshal.Copy(obj.mDataBuffer, 0, outbuffer, lengthElements);

        return FMOD.RESULT.OK;
    }
    private void Start()
    {
     //   Vector3 v = Camera.main.transform.position;
     //   FMOD.VECTOR f = new FMOD.VECTOR { x = v.x, y = v.y, z = v.z };
      //  RuntimeManager.CoreSystem.set3DListenerAttributes(0, ref f, , 0, 0);
    }
    // Start is called before the first frame update
    public void Init(string codecName, uint dspSize)
    {
        // how many capture devices are plugged in for us to use.
        int numOfDriversConnected;
        int numofDrivers;
        
        RuntimeManager.CoreSystem.set3DSettings(0.2f, 0.3f, 1.0f);
        FMOD.RESULT res = RuntimeManager.CoreSystem.getRecordNumDrivers(out numofDrivers, out numOfDriversConnected);

        if (res != FMOD.RESULT.OK)
        {
            Debug.Log("Failed to retrieve driver details: " + res);
            return;
        }

        if (numOfDriversConnected == 0)
        {
            Debug.Log("No capture devices detected!");
            return;
        }
        else
            Debug.Log("You have " + numOfDriversConnected + " capture devices available to record with.");


        // info about the device we're recording with.
        System.Guid micGUID;
        FMOD.DRIVER_STATE driverState;
        FMOD.SPEAKERMODE speakerMode;
        int captureNumChannels;
        int nDriver;
        int nConnected;
        //RuntimeManager.CoreSystem.getRecordNumDrivers(out n)
        RuntimeManager.CoreSystem.getRecordDriverInfo(captureDeviceIndex, out captureDeviceName, 50,
            out micGUID, out CaptureSrate, out speakerMode, out captureNumChannels, out driverState);
        //CaptureSrate = 16000;
        driftThreshold = (uint)(CaptureSrate * DRIFT_MS) / 1000;       /* The point where we start compensating for drift */
        desiredLatency = (uint)(CaptureSrate * LATENCY_MS) / 1000;     /* User specified latency */
        adjustedLatency = (uint)desiredLatency;                      /* User specified latency adjusted for driver update granularity */
        actualLatency = (uint)desiredLatency;                                 /* Latency measured once playback begins (smoothened for jitter) */


        Debug.Log("captureNumChannels of capture device: " + captureNumChannels);
        Debug.Log("captureSrate: " + CaptureSrate);

        encoder = AudioCodecFactory.CreateEncoder(codecName, CaptureSrate, dspSize);

        // create sound where capture is recorded

        exinfo.cbsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = captureNumChannels;
        exinfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;
        exinfo.defaultfrequency = CaptureSrate;
        exinfo.length = (uint)CaptureSrate * sizeof(short) * (uint)captureNumChannels / 8;

        RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER,
            ref exinfo, out sound);

        // start recording    
        RuntimeManager.CoreSystem.recordStart(captureDeviceIndex, sound, true);


        sound.getLength(out soundLength, FMOD.TIMEUNIT.PCM);

        // play sound on dedicated channel in master channel group

        if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) != FMOD.RESULT.OK)
            Debug.LogWarningFormat("FMOD: Unable to create a master channel group: masterCG");

        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG);
        FMODUnity.RuntimeManager.CoreSystem.createChannelGroup("capture", out captureCG);
        masterCG.addGroup(captureCG);
        RuntimeManager.CoreSystem.playSound(sound, captureCG, true, out channel);
        channel.setPaused(true);
        int nchan;
        captureCG.getNumChannels(out nchan);
        Debug.Log("dchan" + nchan);
        // Assign the callback to a member variable to avoid garbage collection
        mReadCallback = CaptureDSPReadCallback;
        // Allocate a data buffer large enough for 8 channels, pin the memory to avoid garbage collection
        uint bufferLength;
        int numBuffers;
        FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out bufferLength, out numBuffers);
        mDataBuffer = new float[bufferLength * 2];
        mBufferLength = bufferLength;

        // Tentatively changed buffer length by calling setDSPBufferSize in file Assets/Plugins/FMOD/src/RuntimeManager.cs	
        // Tentatively changed buffer length by calling setDSPBufferSize in file Assets/Plugins/FMOD/src/fmod.cs - line 1150

        Debug.Log("buffer length:" + bufferLength);

        // Get a handle to this object to pass into the callback
        mObjHandle = GCHandle.Alloc(this);
        //mObjHandle1 = GCHandle.Alloc(this);
        if (mObjHandle != null)
        {
            // Define a basic DSP that receives a callback each mix to capture audio
            FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
            desc.numinputbuffers = 2;
            desc.numoutputbuffers = 2;
            desc.read = mReadCallback;
            desc.userdata = GCHandle.ToIntPtr(mObjHandle);

            // Create an instance of the capture DSP and attach it to the master channel group to capture all audio            
            if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref desc, out mCaptureDSP) == FMOD.RESULT.OK)
            {
               
                if (captureCG.addDSP(0, mCaptureDSP) != FMOD.RESULT.OK)
                {
                    Debug.LogWarningFormat("FMOD: Unable to add mCaptureDSP to the master channel group");
                }
                
                //mCaptureDSP.setBypass(true);
                //Debug.Log("dsp added");
            }
            else
            {
                Debug.LogWarningFormat("FMOD: Unable to create a DSP: mCaptureDSP");
            }
        }
        else
        {
            Debug.LogWarningFormat("FMOD: Unable to create a GCHandle: mObjHandle");
        }
    }
    void OnDestroy()
    {
        captureCG.stop();
        captureCG.removeDSP(mCaptureDSP);
        mCaptureDSP.release();
        captureCG.release();
        sound.release();
        if(mObjHandle.IsAllocated)
        {
            //mObjHandle.Free();
        }

    }
    private void FixedUpdate()
    {
        RuntimeManager.CoreSystem.getRecordPosition(captureDeviceIndex, out uint recordPos);

        uint recordDelta = (recordPos >= lastRecordPos) ? (recordPos - lastRecordPos) : (recordPos + soundLength - lastRecordPos);
        lastRecordPos = recordPos;
        samplesRecorded += recordDelta;

        if (recordDelta != 0 && (recordDelta < minRecordDelta))
        {
            minRecordDelta = recordDelta; /* Smallest driver granularity seen so far */
            adjustedLatency = (recordDelta <= desiredLatency) ? desiredLatency : recordDelta; /* Adjust our latency if driver granularity is high */
        }

        if (!RecordingStarted)
        {
            if (samplesRecorded >= adjustedLatency)
            {
                channel.setPaused(false);
                RecordingStarted = true;
            }
        }
    }
}
