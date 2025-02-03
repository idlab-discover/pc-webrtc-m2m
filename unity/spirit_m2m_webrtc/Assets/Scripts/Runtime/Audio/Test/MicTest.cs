
using System;
using FMODUnity;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Debug = UnityEngine.Debug;

public class MicTest : MonoBehaviour
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
    int captureSrate;
    const int DRIFT_MS = 1;
    const int LATENCY_MS = 50;
    uint driftThreshold;
    uint desiredLatency;
    uint adjustedLatency;
    uint actualLatency;
    uint lastRecordPos = 0;
    uint samplesRecorded = 0;
    uint samplesPlayed = 0;
    uint minRecordDelta = (uint)uint.MaxValue;
    uint lastPlayPos = 0;

    bool recordingStarted = false;
    
    FMOD.ChannelGroup masterCG;
    FMOD.Channel channel;
    FMOD.Sound sound;


    [AOT.MonoPInvokeCallback(typeof(FMOD.DSP_READ_CALLBACK))]
    static FMOD.RESULT CaptureDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));

        IntPtr userData;
        functions.getuserdata(ref dsp_state, out userData);

        GCHandle objHandle = GCHandle.FromIntPtr(userData);
        MicTest obj = objHandle.Target as MicTest;

        Debug.Log("inchannels:" + inchannels);
        Debug.Log("outchannels:" + outchannels);
        Debug.Log("Length: " + length);
       
        // Copy the incoming buffer to process later
        int lengthElements = (int)length * inchannels;
        Marshal.Copy(inbuffer, obj.mDataBuffer, 0, lengthElements);

        // Copy the inbuffer to the outbuffer so we can still hear it
       // Marshal.Copy(obj.mDataBuffer, 0, outbuffer, lengthElements);

        return FMOD.RESULT.OK;
    }
    static FMOD.RESULT PlaybackDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));

        IntPtr userData;
        functions.getuserdata(ref dsp_state, out userData);

        GCHandle objHandle = GCHandle.FromIntPtr(userData);
        MicTest obj = objHandle.Target as MicTest;

        Debug.Log("inchannels:" + inchannels);
        Debug.Log("outchannels:" + outchannels);
        Debug.Log("Length: " + length);

        // Copy the incoming buffer to process later
        int lengthElements = (int)length * inchannels;
        //Marshal.Copy(inbuffer, obj.mDataBuffer, 0, lengthElements);

        // Copy the inbuffer to the outbuffer so we can still hear it
        Marshal.Copy(obj.mDataBuffer, 0, outbuffer, lengthElements);

        return FMOD.RESULT.OK;
    }

    // Start is called before the first frame update
    void Start()
    {
        // how many capture devices are plugged in for us to use.
        int numOfDriversConnected;
        int numofDrivers;
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
        RuntimeManager.CoreSystem.getRecordDriverInfo(captureDeviceIndex, out captureDeviceName, 50,
            out micGUID, out captureSrate, out speakerMode, out captureNumChannels, out driverState);

        driftThreshold = (uint)(captureSrate * DRIFT_MS) / 1000;       /* The point where we start compensating for drift */
        desiredLatency = (uint)(captureSrate * LATENCY_MS) / 1000;     /* User specified latency */
        adjustedLatency = (uint)desiredLatency;                      /* User specified latency adjusted for driver update granularity */
        actualLatency = (uint)desiredLatency;                                 /* Latency measured once playback begins (smoothened for jitter) */


        Debug.Log("captureNumChannels of capture device: " + captureNumChannels);
        Debug.Log("captureSrate: " + captureSrate);


        // create sound where capture is recorded
        exinfo.cbsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = captureNumChannels;
        exinfo.format = FMOD.SOUND_FORMAT.PCM16;
        exinfo.defaultfrequency = captureSrate;
        exinfo.length = (uint)captureSrate * sizeof(short) * (uint)captureNumChannels;

        RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER,
            ref exinfo, out sound);

        // start recording    
        RuntimeManager.CoreSystem.recordStart(captureDeviceIndex, sound, true);


        sound.getLength(out soundLength, FMOD.TIMEUNIT.PCM);

        // play sound on dedicated channel in master channel group

        if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) != FMOD.RESULT.OK)
            Debug.LogWarningFormat("FMOD: Unable to create a master channel group: masterCG");

        FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG);
        RuntimeManager.CoreSystem.playSound(sound, masterCG, true, out channel);
        channel.setPaused(true);
        
        // Assign the callback to a member variable to avoid garbage collection
        mReadCallback = CaptureDSPReadCallback;
        mPlaybackCallback = PlaybackDSPReadCallback;
        // Allocate a data buffer large enough for 8 channels, pin the memory to avoid garbage collection
        uint bufferLength;
        int numBuffers;
        FMODUnity.RuntimeManager.CoreSystem.getDSPBufferSize(out bufferLength, out numBuffers);
        mDataBuffer = new float[bufferLength * 8];
        mBufferLength = bufferLength;

        // Tentatively changed buffer length by calling setDSPBufferSize in file Assets/Plugins/FMOD/src/RuntimeManager.cs	
        // Tentatively changed buffer length by calling setDSPBufferSize in file Assets/Plugins/FMOD/src/fmod.cs - line 1150

        Debug.Log("buffer length:" + bufferLength);

        // Get a handle to this object to pass into the callback
        mObjHandle = GCHandle.Alloc(this);
        mObjHandle1 = GCHandle.Alloc(this);
        if (mObjHandle != null)
        {
            // Define a basic DSP that receives a callback each mix to capture audio
            FMOD.DSP_DESCRIPTION desc = new FMOD.DSP_DESCRIPTION();
            desc.numinputbuffers = 2;
            desc.numoutputbuffers = 0;
            desc.read = mReadCallback;
            desc.userdata = GCHandle.ToIntPtr(mObjHandle);

            // Create an instance of the capture DSP and attach it to the master channel group to capture all audio            
            if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref desc, out mCaptureDSP) == FMOD.RESULT.OK)
            {
                if (masterCG.addDSP(0, mCaptureDSP) != FMOD.RESULT.OK)
                {
                    Debug.LogWarningFormat("FMOD: Unable to add mCaptureDSP to the master channel group");
                }
            }
            else
            {
                Debug.LogWarningFormat("FMOD: Unable to create a DSP: mCaptureDSP");
            }

            // Define a basic DSP that receives a callback each mix to capture audio
            FMOD.DSP_DESCRIPTION descPlayback = new FMOD.DSP_DESCRIPTION();
            descPlayback.numinputbuffers = 0;
            descPlayback.numoutputbuffers = 2;
            descPlayback.read = mPlaybackCallback;
            descPlayback.userdata = GCHandle.ToIntPtr(mObjHandle1);
            
            // Create an instance of the capture DSP and attach it to the master channel group to capture all audio            
            if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref descPlayback, out mPlaybackDSP) == FMOD.RESULT.OK)
            {
                if (channel.addDSP(0, mPlaybackDSP) != FMOD.RESULT.OK)
                {
                    Debug.LogWarningFormat("FMOD: Unable to add mCaptureDSP to the master channel group");
                }
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

        if (!recordingStarted)
        {
            if (samplesRecorded >= adjustedLatency)
            {
                channel.setPaused(false);
                recordingStarted = true;
            }
        }

        /*
            Delay playback until our desired latency is reached.
        */
        if (recordingStarted)
        {
            sound.@lock(recordPos, soundLength, out IntPtr one, out IntPtr two, out uint lone, out uint ltwo);
            int lengthElements = (int)soundLength * 1;
            Debug.Log($"TEST {lengthElements} {mDataBuffer.Length} {lone} {ltwo}");
            // Marshal.Copy(one, mDataBuffer, 0, lengthElements);

            /*
                Stop playback if recording stops.
            */
            RuntimeManager.CoreSystem.isRecording(captureDeviceIndex, out bool isRecording);

            if (!isRecording)
            {
                channel.setPaused(true);
            }

            /*
                Determine how much has been played since we last checked.
            */
            channel.getPosition(out uint playPos, FMOD.TIMEUNIT.PCM);

            uint playDelta = (playPos >= lastPlayPos) ? (playPos - lastPlayPos) : (playPos + soundLength - lastPlayPos);
            lastPlayPos = playPos;
            samplesPlayed += playDelta;

            /*
                Compensate for any drift.
            */
            uint latency = samplesRecorded - samplesPlayed;
            actualLatency = (uint)((0.97f * actualLatency) + (0.03f * latency));

            int playbackRate = captureSrate;
            if (actualLatency < (int)(adjustedLatency - driftThreshold))
            {
                /* Play position is catching up to the record position, slow playback down by 2% */
                playbackRate = captureSrate - (captureSrate / 50);
            }
            else if (actualLatency > (int)(adjustedLatency + driftThreshold))
            {
                /* Play position is falling behind the record position, speed playback up by 2% */
                playbackRate = captureSrate + (captureSrate / 50);
            }

            channel.setFrequency((float)playbackRate);
        }

    }
}
