using System;
using System.Runtime.InteropServices;
using UnityEngine;
using FMODUnity;
using FMOD;
using Debug = UnityEngine.Debug;

public class FMODMicrophoneStream : MonoBehaviour
{
    private FMOD.Sound micSound;
    private FMOD.Channel channel;
    private FMOD.System system;
    private byte[] micBuffer;
    private int micBufferPosition = 0;
    private uint micBufferLength;
    private bool isPlaying = false;
    FMOD.ChannelGroup mCG;
    private void Start()
    {
        // Initialize FMOD system
        system = FMODUnity.RuntimeManager.CoreSystem;

        // Get the default microphone device
        int numOfDriversConnected;
        int numDrivers;
        system.getRecordNumDrivers(out numDrivers, out numOfDriversConnected);

        if (numDrivers == 0)
        {
            Debug.LogError("No recording devices found!");
            return;
        }

        // Create a microphone recording sound
        FMOD.CREATESOUNDEXINFO soundInfo = new FMOD.CREATESOUNDEXINFO();
        soundInfo.cbsize = Marshal.SizeOf(typeof(FMOD.CREATESOUNDEXINFO));
        soundInfo.numchannels = 1; // Mono
        soundInfo.defaultfrequency = 44100; // Sample rate
        soundInfo.format = FMOD.SOUND_FORMAT.PCM16;

        system.createSound("", FMOD.MODE.LOOP_NORMAL | FMOD.MODE.OPENUSER, ref soundInfo, out micSound);

        micBufferLength = (uint)(soundInfo.defaultfrequency * soundInfo.numchannels * 2); // 2 bytes per sample
        micBuffer = new byte[micBufferLength];

        // Start recording
        system.recordStart(0, micSound, true);
        
        system.getMasterChannelGroup(out FMOD.ChannelGroup mCG);
        // Play the microphone input
        system.playSound(micSound, mCG, false, out channel);
        isPlaying = true;

        Debug.Log("Microphone recording started and playing...");
    }

    private void Update()
    {
        if (!isPlaying) return;

        // Get the recording position
        uint recordPosition;
        system.getRecordPosition(0, out recordPosition);

        // Check for new audio data
        if (recordPosition != micBufferPosition)
        {
            uint bytesToRead = (recordPosition > micBufferPosition)
                ? recordPosition - (uint)micBufferPosition
                : micBufferLength - (uint)micBufferPosition + recordPosition;

            if (bytesToRead > 0)
            {
                // Lock the microphone buffer
                IntPtr ptr1, ptr2;
                uint len1, len2;
                micSound.@lock((uint)micBufferPosition, bytesToRead, out ptr1, out ptr2, out len1, out len2);

                // Copy microphone data to local buffer
                if (ptr1 != IntPtr.Zero)
                {
                    Marshal.Copy(ptr1, micBuffer, micBufferPosition, (int)len1);
                    micBufferPosition = (int)((micBufferPosition + len1) % micBufferLength);
                }
                if (ptr2 != IntPtr.Zero)
                {
                    Marshal.Copy(ptr2, micBuffer, micBufferPosition, (int)len2);
                    micBufferPosition = (int)((micBufferPosition + len2) % micBufferLength);
                }

                // Unlock the buffer
                micSound.unlock(ptr1, ptr2, len1, len2);
            }
        }
    }

    private void OnDestroy()
    {
        // Stop recording and release resources
        if (micSound.hasHandle())
        {
            system.recordStop(0);
            micSound.release();
        }
        Debug.Log("Microphone recording stopped and resources released.");
    }
}
