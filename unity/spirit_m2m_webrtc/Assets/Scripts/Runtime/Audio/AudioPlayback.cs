
using System;
using FMODUnity;
using UnityEngine;
using System.Runtime.InteropServices;
using System.Diagnostics;
using Debug = UnityEngine.Debug;
using FMOD;

public class AudioPlayback : MonoBehaviour
{
    //public variables
    [Header("Capture Device details")]
    public int captureDeviceIndex = 0;
    [TextArea] public string captureDeviceName = null;

    FMOD.CREATESOUNDEXINFO exinfo;

    private AudioPlaybackBuffer buffer;

    // Custom DSPCallback variables 

    private FMOD.DSP_READ_CALLBACK mReadCallback;
    private FMOD.DSP_READ_CALLBACK mPlaybackCallback;
    private FMOD.Studio.EVENT_CALLBACK mEventCallback;
    private FMOD.DSP mCaptureDSP;
    private FMOD.DSP mPlaybackDSP;
    public float[] mDataBuffer;
    private GCHandle mObjHandle;
    private uint mBufferLength;
    private uint soundLength;
    int captureSrate;
    const int DRIFT_MS = 1;
    const int LATENCY_MS = 50;
    uint driftThreshold;
    uint desiredLatency;
    uint adjustedLatency;
    uint actualLatency;
    uint samplesPlayed = 0;
    uint minRecordDelta = (uint)uint.MaxValue;
    uint lastPlayPos = 0;
    uint samplesRecorded = 0;
    bool isPlaying = false;

    FMOD.ChannelGroup masterCG;
    FMOD.ChannelGroup playbackCG;
    FMOD.Channel channel;
    FMOD.Sound sound;
    FMOD.Channel ch;
    FMOD.Studio.EventInstance voiceInstance;

    private AudioDecoder dec;
    private uint dspSize;
    static FMOD.RESULT PlaybackDSPReadCallback(ref FMOD.DSP_STATE dsp_state, IntPtr inbuffer, IntPtr outbuffer, uint length, int inchannels, ref int outchannels)
    {
        FMOD.DSP_STATE_FUNCTIONS functions = (FMOD.DSP_STATE_FUNCTIONS)Marshal.PtrToStructure(dsp_state.functions, typeof(FMOD.DSP_STATE_FUNCTIONS));

        IntPtr userData;
        functions.getuserdata(ref dsp_state, out userData);

        GCHandle objHandle = GCHandle.FromIntPtr(userData);
        AudioPlayback obj = objHandle.Target as AudioPlayback;

        Debug.Log("inchannelsPlay:" + inchannels);
        Debug.Log("outchannelsPlay:" + outchannels);
        Debug.Log("LengthPlay: " + length);

        // Copy the incoming buffer to process later
        int lengthElements = (int)length * inchannels;
        //Marshal.Copy(inbuffer, obj.mDataBuffer, 0, lengthElements);

        // Copy the inbuffer to the outbuffer so we can still hear it
        //Marshal.Copy(obj.mDataBuffer, 0, outbuffer, lengthElements);
       // Array.Clear(obj.mDataBuffer, 0, obj.mDataBuffer.Length);
        //obj.ch.setFrequency(24000);
        return FMOD.RESULT.OK;
    }
    static FMOD.RESULT VoiceEventCallback(FMOD.Studio.EVENT_CALLBACK_TYPE type, IntPtr _event, IntPtr parameters)
    {
        Debug.Log("cab");
        FMOD.Studio.EventInstance eventInstance = new FMOD.Studio.EventInstance(_event);

        IntPtr userData;
        eventInstance.getUserData(out userData);

        GCHandle objHandle = GCHandle.FromIntPtr(userData);
        AudioPlayback obj = objHandle.Target as AudioPlayback;
        if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.CREATE_PROGRAMMER_SOUND)
        {
            Debug.Log("pgm");
            FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES pms = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameters, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));
            pms.sound = obj.sound.handle;

            FMOD.Studio.SOUND_INFO info;
            //    FMODUnity.RuntimeManager.StudioSystem.getSoundInfo(sound.get, out info);
            // pms.subsoundIndex = 1;
            Marshal.StructureToPtr(pms, parameters, false);
        }
        else if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.DESTROY_PROGRAMMER_SOUND)
        {
            Debug.Log("destroy");
        }
        else if (type == FMOD.Studio.EVENT_CALLBACK_TYPE.TIMELINE_MARKER)
        {
            Debug.Log("sound play");
            eventInstance.getChannelGroup(out obj.playbackCG);
            FMOD.ChannelGroup cg;

            obj.playbackCG.getGroup(0, out cg);
            cg.getChannel(0, out obj.ch);
            // eventInstance.getChannelGroup(out obj.playbackCG);
            // RuntimeManager.CoreSystem.playSound(obj.sound, obj.playbackCG, true, out obj.ch);
            // Define a basic DSP that receives a callback each mix to capture audio
            /* FMOD.DSP_DESCRIPTION descPlayback = new FMOD.DSP_DESCRIPTION();
             descPlayback.numinputbuffers = 0;
             descPlayback.numoutputbuffers = 2;
             descPlayback.read = PlaybackDSPReadCallback;
             descPlayback.userdata = userData;

             obj.mPlaybackDSP.addInput(obj.mPlaybackDSP);
             //  obj.sound.
             // Create an instance of the capture DSP and attach it to the master channel group to capture all audio            
             if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref descPlayback, out obj.mPlaybackDSP) == FMOD.RESULT.OK)
             {
                 obj.mPlaybackDSP.setChannelFormat(FMOD.CHANNELMASK.STEREO, 2, FMOD.SPEAKERMODE.STEREO);
                 int nchan = 0;
                 eventInstance.setPitch(0.5f);

                 eventInstance.getChannelGroup(out obj.playbackCG);
                 FMOD.ChannelGroup cg;

                 obj.playbackCG.getGroup(0, out cg);
                 FMOD.DSP d;
                 obj.playbackCG.getDSP(0, out d);
                 //FMOD.Channel ch;

                 cg.getChannel(0, out obj.ch);
                 obj.playbackCG.setPitch(0.5f);

                 cg.setPitch(0.5f);
                 obj.ch.setPitch(0.5f);

                 //cg.getds
                 Debug.Log("nchan" + nchan);
                 if (obj.ch.addDSP(0, obj.mPlaybackDSP) != FMOD.RESULT.OK)
                 {
                     Debug.LogWarningFormat("FMOD: Unable to add mPlaybackDSP to the master channel group");
                 }
                 cg.getNumChannels(out nchan);
                 Debug.Log("nchan" + nchan);

             }
             else
             {
                 Debug.LogWarningFormat("FMOD: Unable to create a DSP: mCaptureDSP");
             }*/
        }
        //  FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES pms = new FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES(parameters);
        // FMOD.Studio.EventInstance eventInstance = (FMOD.Studio.EventInstance)Marshal.PtrToStructure(_event, typeof(FMOD.Studio.EventInstance));
        // FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES pms = (FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES)Marshal.PtrToStructure(parameters, typeof(FMOD.Studio.PROGRAMMER_SOUND_PROPERTIES));
        //pms.sound = sound.handle;

        return FMOD.RESULT.OK;
    }
    // Start is called before the first frame update
    public void Init(int capRate, AudioPlaybackParams pms)
    {
        Debug.Log("Audio playback inited");

        dec = AudioCodecFactory.CreateDecoder(pms.codecName, capRate, pms.dspSize);
        
        this.dspSize = pms.dspSize;
        captureSrate = capRate;
        audioLength = captureSrate * sizeof(short) * 2;
        buffer = new AudioPlaybackBuffer((uint)audioLength, pms);
        voiceInstance = FMODUnity.RuntimeManager.CreateInstance("event:/voiceStream");
        FMODUnity.RuntimeManager.AttachInstanceToGameObject(voiceInstance, GetComponent<Transform>(), GetComponent<Rigidbody>());
        //voiceInstance.setUserData();
        voiceInstance.setCallback(VoiceEventCallback);

        // how many capture devices are plugged in for us to use.
        

        // info about the device we're recording with.
        driftThreshold = (uint)(captureSrate * DRIFT_MS) / 1000;       /* The point where we start compensating for drift */
        desiredLatency = (uint)(captureSrate * LATENCY_MS) / 1000;     /* User specified latency */
        adjustedLatency = (uint)desiredLatency;                      /* User specified latency adjusted for driver update granularity */
        actualLatency = (uint)desiredLatency;                                 /* Latency measured once playback begins (smoothened for jitter) */

        if (FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG) != FMOD.RESULT.OK)
            Debug.LogWarningFormat("FMOD: Unable to create a master channel group: masterCG");

        // FMODUnity.RuntimeManager.CoreSystem.getMasterChannelGroup(out masterCG);
        // FMODUnity.RuntimeManager.CoreSystem.createChannelGroup("", out playbackCG);
        //masterCG.addGroup(playbackCG);
        exinfo.cbsize = System.Runtime.InteropServices.Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        exinfo.numchannels = 2;
        exinfo.format = FMOD.SOUND_FORMAT.PCMFLOAT;
        exinfo.defaultfrequency = captureSrate;
        
        exinfo.length = (uint)audioLength;
        RuntimeManager.CoreSystem.createSound(exinfo.userdata, FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER | FMOD.MODE.NONBLOCKING | FMOD.MODE._3D,
            ref exinfo, out sound);
        // RuntimeManager.CoreSystem.playSound(sound, playbackCG, true, out channel);
        //channel.setPaused(true);

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
        if (mObjHandle != null)
        {
            // Define a basic DSP that receives a callback each mix to capture audio
            /*FMOD.DSP_DESCRIPTION descPlayback = new FMOD.DSP_DESCRIPTION();
            descPlayback.numinputbuffers = 0;
            descPlayback.numoutputbuffers = 2;
            descPlayback.read = mPlaybackCallback;
            descPlayback.userdata = GCHandle.ToIntPtr(mObjHandle);*/
            voiceInstance.setUserData(GCHandle.ToIntPtr(mObjHandle));
            voiceInstance.start();
            // Create an instance of the capture DSP and attach it to the master channel group to capture all audio            
            /*if (FMODUnity.RuntimeManager.CoreSystem.createDSP(ref descPlayback, out mPlaybackDSP) == FMOD.RESULT.OK)
            {
                mPlaybackDSP.setChannelFormat(FMOD.CHANNELMASK.STEREO, 2, FMOD.SPEAKERMODE.STEREO);
                
                if (playbackCG.addDSP(0, mPlaybackDSP) != FMOD.RESULT.OK)
                {
                    Debug.LogWarningFormat("FMOD: Unable to add mPlaybackDSP to the master channel group");
                }
               
            }
            else
            {
                Debug.LogWarningFormat("FMOD: Unable to create a DSP: mCaptureDSP");
            }*/
        }
        else
        {
            Debug.LogWarningFormat("FMOD: Unable to create a GCHandle: mObjHandle");
        }
    }
    void OnDestroy()
    {
        voiceInstance.release();
        ch.removeDSP(mPlaybackDSP);
        mPlaybackDSP.release();
        sound.release();
        if (mObjHandle.IsAllocated)
        {
            // mObjHandle.Free();
        }



    }
    private void FixedUpdate()
    {
        /*
            Delay playback until our desired latency is reached.
        */
        if (isPlaying)
        {
            /*
                Determine how much has been played since we last checked.
            */
            /*channel.getPosition(out uint playPos, FMOD.TIMEUNIT.PCM);

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

            // channel.setFrequency((float)playbackRate);

            if (ch.hasHandle())
            {
                //ch.setFrequency(46500);
            }
            
        }

    }
    public void StartPlayback()
    {
        if (!isPlaying)
        {
            Debug.Log("Playback started");
            // channel.setPaused(false);
            isPlaying = true;
        }

    }
    public void StopPlayback()
    {
        if (isPlaying)
        {
            //  channel.setPaused(true);
            isPlaying = false;
        }
    }
    private int audioCounter;
    private int audioLength;

    public void DecodeAndCopyToBuffer(byte[] receivedData)
    {
        if (audioLength == 0)
        {
            return;
        }
        AudioFrame af = dec.DecodeSample(receivedData);
        CopyToBuffer(af.Timestamp, af.FrameNr, af.AudioData);

    }
    //private uint frameNr;
    // TODO fix starting point => current position audio track
    //      fid static sometimes
    public void CopyToBuffer(ulong timestamp, UInt32 frameNr, float[] receivedData)
    {
        if(audioLength == 0)
        {
            return;
        }
        buffer.AddItem(timestamp, frameNr, receivedData);
        Debug.Log("[AUDIO] frame number " + frameNr + " " + timestamp);
        if(frameNr >= 500 && !buffer.PlaybackStarted)
        {
            buffer.Sound = sound;
            buffer.Channel = ch;
            buffer.ForceStartPlayback();
        }
       /* uint playPos = 0;
        ch.getPosition(out playPos, FMOD.TIMEUNIT.PCM);
        Debug.Log("copy " + playPos);
    //    receivedData.CopyTo(mDataBuffer, 0);
        int lengthToRead = receivedData.Length*sizeof(float) / 4;
        IntPtr ptr1, ptr2;
        uint lenBytes1, lenBytes2;
        var res = sound.@lock((uint)audioCounter, (uint)lengthToRead, out ptr1, out ptr2, out lenBytes1, out lenBytes2);
        if (lenBytes1 > 0)
        {
            Marshal.Copy(receivedData, 0, ptr1, (int)lenBytes1 / sizeof(float));
        }
        if (lenBytes2 > 0)
        {
            Marshal.Copy(receivedData, (int)lenBytes1 / sizeof(float), ptr2, (int)lenBytes2 / sizeof(float));
        }

       // sound.unlock(ptr1, ptr2, lenBytes1, lenBytes2);
        audioCounter = (audioCounter + lengthToRead) % audioLength;
        Debug.Log("Audio Counter" + audioCounter);
        //samplesRecorded += (uint)receivedData.Length;*/
    }

    public void SetTimestampLatestPC(ulong timestamp)
    {
        buffer.LatestPcTimestamp = timestamp;
        if(!buffer.PlaybackStarted)
        {
            //buffer.StartPlayback();
            // TODO change to start playback
            buffer.ForceStartPlayback();
        }
    }
}
