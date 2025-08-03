using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

using UnityEngine;
using UnityEngine.Rendering;

public class PCReceiver : MonoBehaviour
{
    public AudioPlayback AudioPlaybackPrefab;
    public AudioPlaybackParams AudioParams;
    public uint ClientID;
    public int NDescriptions;
    public bool useAudio;
    private RenderablePointCloudPipeline pipeline;
    private List<bool> activeDescriptions = new List<bool> { false, false, false };
    private List<System.Threading.Thread> workerThreads = new List<System.Threading.Thread>();
    private System.Threading.Thread audioThread;
    //  private List<ConcurrentQueue<DecodedPointCloudData>> queues = new List<ConcurrentQueue<DecodedPointCloudData>>();

    private Dictionary<uint, DecodedPointCloudData> inProgessFrames;
    private ConcurrentQueue<DecodedPointCloudData> queue;

    private IntPtr rawConverter;
    private IntPtr colorDecoder;
    private IntPtr depthDecoder;
    private Dictionary<UInt32, DecodedRawFrame> inProgessFramesRaw;
    private ConcurrentQueue<DecodedRawFrame> queueRaw;

    private static Mutex mut = new Mutex();
    private int lastCompletedFrameNr = -1;
    
    private bool keep_working = true;

    // ####################### Unity GameObjects #########################
    public List<GameObject> PCRenderers;
    Mesh currentMesh;
    private List<MeshFilter> meshFilters;
    private AudioPlayback audioPlayback;
    // Start is called before the first frame update
    void Start()
    {
       
        meshFilters = new(PCRenderers.Count);
        foreach(var p in PCRenderers)
        {
            meshFilters.Add(p.GetComponent<MeshFilter>());
        }
        queue = new();
        inProgessFrames = new();
        queueRaw = new();
        inProgessFramesRaw = new();

        // activeDescriptions = new(NDescriptions);
        for (int i = 0; i < NDescriptions; i++)
        {
            activeDescriptions.Add(false);
        }
        for (int i = 0; i < NDescriptions; i++)
        {
            int descriptionID = i; // Copy as thread starts later but still uses reference to i
            workerThreads.Add(new System.Threading.Thread(() =>
            {
                Debug.Log($"CREAETING {descriptionID}");
                pollDescription((uint)descriptionID);
            }));
            workerThreads[i].Start();
        }
        Debug.Log("audio used" + AudioParams.useAudio);
        if(AudioParams.useAudio)
        {
            audioPlayback = Instantiate(AudioPlaybackPrefab, transform);
            audioPlayback.Init(48000, AudioParams);
            audioThread = new System.Threading.Thread(() =>
            {
                pollAudio(ClientID, audioPlayback);
            });
            audioThread.Start();
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (!queue.IsEmpty)
        {
            DecodedPointCloudData dec = null;
            bool succes = false;
            while(!queue.IsEmpty)
            {
                succes = queue.TryDequeue(out dec);
            }
            
            if (succes)
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
                int rendererIndex = qualityToRenderIndex(dec.Quality);
                for(int i = 0; i < PCRenderers.Count; i++)
                {
                    if(i == rendererIndex)
                    {
                        PCRenderers[i].SetActive(true);
                        meshFilters[i].mesh = currentMesh;
                    } else
                    {
                        PCRenderers[i].SetActive(false);
                    }
                   
                }
                if(AudioParams.useAudio)
                {
                    //audioPlayback.SetTimestampLatestPC(dec.Timestamp);
                }
                
            }
        } else
        {
            if (!queueRaw.IsEmpty)
            {
                DecodedRawFrame dec = null;
                bool succes = false;
                while (!queueRaw.IsEmpty)
                {
                    succes = queueRaw.TryDequeue(out dec);
                }

                if (succes)
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
                    int rendererIndex = qualityToRenderIndex(100);
                    for (int i = 0; i < PCRenderers.Count; i++)
                    {
                        if (i == rendererIndex)
                        {
                            PCRenderers[i].SetActive(true);
                            meshFilters[i].mesh = currentMesh;
                        }
                        else
                        {
                            PCRenderers[i].SetActive(false);
                        }

                    }
                    if (AudioParams.useAudio)
                    {
                        audioPlayback.SetTimestampLatestPC(dec.Timestamp);
                    }

                }
            }
        }
       
    }
    void OnDestroy()
    {
        if (rawConverter != IntPtr.Zero)
        {
            Realsense2Invoker.free_raw_converter(rawConverter);
            rawConverter = IntPtr.Zero;
        }
        if(colorDecoder != IntPtr.Zero)
        {
            RawInvoker.free_color_decoder(colorDecoder); 
            colorDecoder = IntPtr.Zero;
        }
        if (depthDecoder != IntPtr.Zero)
        {
            RawInvoker.free_depth_decoder(depthDecoder);
            depthDecoder = IntPtr.Zero;
        }
        for (int i = 0;i < NDescriptions;i++)
        {
       //     workerThreads[i].Join();
        }    
    }
    private unsafe void decodeDracoFrame(byte* ptr, byte[] messageBuffer, uint clientID, uint descriptionID, UInt64 timestamp, uint descriptionFrameNr, int descriptionSize)
    {
        IntPtr decoderPtr = IntPtr.Zero;
        Debug.Log($"Start decoding");
        decoderPtr = DracoInvoker.decode_pc(ptr + 20, (uint)descriptionSize);
        Debug.Log($"Decoding done");
        if (decoderPtr == IntPtr.Zero)
        {
            Debug.Log($"Debug error at client {ClientID} for description {descriptionID}");
            return;
        }
        mut.WaitOne();
        DecodedPointCloudData pcData;
        if (!inProgessFrames.TryGetValue(descriptionFrameNr, out pcData))
        {
            int nTotalPointsInFrame = BitConverter.ToInt32(messageBuffer, 12);
            pcData = new DecodedPointCloudData(descriptionFrameNr, nTotalPointsInFrame, NDescriptions, activeDescriptions, timestamp);
            inProgessFrames.Add(descriptionFrameNr, pcData);
        }
        UInt32 nDecodedPoints = DracoInvoker.get_n_points(decoderPtr);
        IntPtr pointsPtr = DracoInvoker.get_point_array(decoderPtr);
        IntPtr colorPtr = DracoInvoker.get_color_array(decoderPtr);
        float* pointsUnsafePtr = (float*)pointsPtr;
        byte* colorsUnsafePtr = (byte*)colorPtr;

        for (int i = 0; i < nDecodedPoints; i++)
        {
            //    points[i] = new Vector3(0, 0, 0);
            if (pointsUnsafePtr[(i * 3)] == 0 && pointsUnsafePtr[(i * 3) + 1] == 0 && pointsUnsafePtr[(i * 3) + 2] == 0)
            {
                
                continue;
            }
            pcData.Points.Add(new Vector3(pointsUnsafePtr[(i * 3)] * -1, pointsUnsafePtr[(i * 3) + 1] * -1, pointsUnsafePtr[(i * 3) + 2] * -1));
            pcData.Colors.Add(new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255));
        }
        DracoInvoker.free_decoder(decoderPtr);
        Debug.Log($"Decoders freed");
        pcData.CompletionStatus[(int)descriptionID] = true;
        pcData.CurrentNDescriptions++;
        pcData.Quality += descToQual(descriptionID);
        if (pcData.IsCompleted)
        {
            if (descriptionFrameNr % 10 == 0)
            {
                Debug.Log($"Frame {descriptionFrameNr} completed, last compl= {lastCompletedFrameNr}");
            }

            inProgessFrames.Remove(descriptionFrameNr);
            if (descriptionFrameNr >= lastCompletedFrameNr)
            {
                lastCompletedFrameNr = (int)pcData.FrameNr;
                queue.Enqueue(pcData);
            }

        }
        mut.ReleaseMutex();
    }

    private unsafe void decodeDepthFrame(byte* ptr, byte[] messageBuffer, UInt64 timestamp, uint frameNr)
    {
        
        if (rawConverter != IntPtr.Zero)
        {
            mut.WaitOne();
            uint codecType = BitConverter.ToUInt32(messageBuffer, 16);
            if (depthDecoder == IntPtr.Zero)
            {
                depthDecoder = RawInvoker.create_depth_decoder((DepthCodecType)codecType);
            }
            uint nPoints = BitConverter.ToUInt32(messageBuffer, 20);
            uint width = BitConverter.ToUInt32(messageBuffer, 24);
            uint height = BitConverter.ToUInt32(messageBuffer, 28);
            uint size = BitConverter.ToUInt32(messageBuffer, 32);

            IntPtr decoded_depth = RawInvoker.decode_depth(depthDecoder, new IntPtr(ptr + 36), width, height);
           
            DecodedRawFrame rawData;
            if (!inProgessFramesRaw.TryGetValue((uint)frameNr, out rawData))
            {
                rawData = new DecodedRawFrame(frameNr, nPoints, timestamp);
                inProgessFramesRaw.Add((uint)frameNr, rawData);
            }
            rawData.DecodedDepth = decoded_depth;
            rawData.DepthCompleted = true;
       
          
            if (rawData.IsCompleted)
            {
                completeRawFrame(rawData);
            }
            mut.ReleaseMutex();
        }
    }

    private unsafe void decodeColorFrame(byte* ptr, byte[] messageBuffer, UInt64 timestamp, uint frameNr)
    {
        
        if (rawConverter != IntPtr.Zero)
        {
            mut.WaitOne();
            // Get codec type
            // Check if codec == same
            uint codecType = BitConverter.ToUInt32(messageBuffer, 16);
            if (colorDecoder == IntPtr.Zero)
            {
                colorDecoder = RawInvoker.create_color_decoder((ColorCodecType)codecType);
            }
        
            uint nPoints = BitConverter.ToUInt32(messageBuffer, 20);
            uint width = BitConverter.ToUInt32(messageBuffer, 24);
            uint height = BitConverter.ToUInt32(messageBuffer, 28);
            uint size = BitConverter.ToUInt32(messageBuffer, 32);
            Debug.Log(nPoints + " " + width + " " + height + " " + size);
            IntPtr decoded_color = RawInvoker.decode_color(colorDecoder, new IntPtr(ptr+36), size, width, height);

             DecodedRawFrame rawData;
             if (!inProgessFramesRaw.TryGetValue((uint)frameNr, out rawData))
             {
                // rawData = new DecodedRawFrame(frameNr, (int)nPoints, timestamp);
                 inProgessFramesRaw.Add((uint)frameNr, rawData);
             }
          
             rawData.DecodedColor = decoded_color;
             rawData.ColorsCompleted = true;
       
            if (rawData.IsCompleted)
             {
                 completeRawFrame(rawData);
             }
             mut.ReleaseMutex();
        }
    }

    private void completeRawFrame(DecodedRawFrame rawFrame)
    {
        
            IntPtr buf_depth = RawInvoker.get_decoded_depth_data(rawFrame.DecodedDepth);
            IntPtr buf_color = RawInvoker.get_decoded_color_data(rawFrame.DecodedColor);
            GCHandle hDepth = GCHandle.Alloc(rawFrame.Points, GCHandleType.Pinned);
            GCHandle hColor = GCHandle.Alloc(rawFrame.Colors, GCHandleType.Pinned);
            try
            {
                Realsense2Invoker.convert_raw_frame(rawConverter, buf_depth, buf_color, hDepth.AddrOfPinnedObject(), hColor.AddrOfPinnedObject());
            }
            finally
            {
                hDepth.Free();
                hColor.Free();
            }
          
            RawInvoker.free_decoded_color(rawFrame.DecodedColor);
            RawInvoker.free_decoded_depth(rawFrame.DecodedDepth);
            if (rawFrame.FrameNr % 100 == 0)
            {
                Debug.Log("Frame done2: " + rawFrame.FrameNr + " " + ((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - rawFrame.Timestamp));
            }
            inProgessFramesRaw.Remove((uint)rawFrame.FrameNr);
            queueRaw.Enqueue(rawFrame);
        
    }

    private void pollDescription(uint descriptionID)
    {
        WebRTCInvoker.wait_for_peer();
        while (keep_working)
        {
            Debug.Log("Polling size");
            int descriptionSize = WebRTCInvoker.get_tile_size(ClientID, 0, descriptionID);
            
            if (descriptionSize == 0)
            {
                keep_working = false;
                Debug.Log("Got no tile");
                continue;
            }
            Debug.Log("Got a tile");
            byte[] messageBuffer = new byte[descriptionSize];
            
            //int descriptionFrameNr = WebRTCInvoker.get_tile_frame_number(ClientID, descriptionID);
            unsafe
            {
                fixed (byte* ptr = messageBuffer)
                {
                    WebRTCInvoker.retrieve_tile(ptr, (uint)descriptionSize, ClientID, 0, descriptionID);
                    UInt64 timestamp = BitConverter.ToUInt64(messageBuffer, 0); ;
                    uint descriptionFrameNr = BitConverter.ToUInt32(messageBuffer, 8);
                    continue;
                    if(descriptionFrameNr <= lastCompletedFrameNr)
                    {
                        continue;
                    }
                    uint frameCodec = BitConverter.ToUInt32(messageBuffer, 12);
                    if((FrameCodec)frameCodec == FrameCodec.Draco)
                    {
                        decodeDracoFrame(ptr, messageBuffer, ClientID, descriptionID, timestamp, descriptionFrameNr, descriptionSize);
                    } else
                    {
                        switch(descriptionID)
                        {
                            case 0: {
                                    decodeDepthFrame(ptr, messageBuffer, timestamp, descriptionFrameNr);
                                    break;
                            };
                            case 1:
                            {
                                    decodeColorFrame(ptr, messageBuffer, timestamp, descriptionFrameNr);
                                    break;
                            }
                        }
                    }
                    
                // queues[(int)descriptionID].Enqueue(new DecodedPointCloudData(points, colors));
                }
            }              
        }
    }
    private bool audioPrevAdded = false;
    public void OnTrackChange(uint frameNr, int descriptionID, bool isAdded)
    {
        mut.WaitOne();
        Debug.Log("[TRACK CHANGE]:" + descriptionID);
        if(descriptionID == 99)
        {
            if(isAdded)
            {
                if(audioPrevAdded)
                {
                    audioPlayback.RestartPlaying();
                }
                audioPrevAdded = true;
            } else
            {
                audioPlayback.StopPlaying();
            }
            mut.ReleaseMutex();
            return;
        }
        activeDescriptions[descriptionID] = isAdded;
        if (isAdded)
        {

        } else
        {
            List<uint> toRemove = new();
            foreach (var fr in inProgessFrames)
            {
                if(fr.Key > frameNr)
                {
                    fr.Value.CompletionStatus[descriptionID] = true;
                    if (fr.Value.IsCompleted)
                    {
                        toRemove.Add(fr.Key);
                        if(fr.Key > lastCompletedFrameNr)
                        {
                            lastCompletedFrameNr = (int)fr.Value.FrameNr;
                            queue.Enqueue(fr.Value);
                        }
                        
                    }
                }
            }
            foreach (var i in toRemove)
            {
                inProgessFrames.Remove(i);
            }
        }
        mut.ReleaseMutex();
    }
    public void OnIntrisicsUpdated(UInt32 capturerID, UInt32 capturerType, IntPtr data)
    {
        if (rawConverter != IntPtr.Zero)
        {
            Realsense2Invoker.free_raw_converter(rawConverter);
            rawConverter = IntPtr.Zero;
        }
        rawConverter = Realsense2Invoker.create_new_raw_converter((CaptureType)capturerType, data);
    }
    private int descToQual(uint dscNr)
    {
        switch(dscNr)
        {
            case 0:
                return 60;
            case 1:
                return 25;
            case 2:
                return 15;
        }
        return 0;
    }
    private int qualityToRenderIndex(int quality)
    {
        switch(quality)
        {
            case 100:
            case 85:
            case 75:
            case 60:
                return 0;
            case 40:
                return 1;
            case 25:
                return 2;
            case 15:
                return 3;
        }
        return -1;
    }

    private void pollAudio(uint clientID, AudioPlayback pb)
    {
        WebRTCInvoker.wait_for_peer();
        while (keep_working)
        {
            Debug.Log("Polling audio size");
            int audioSize = WebRTCInvoker.get_audio_size(clientID);

            if (audioSize == 0)
            {
                keep_working = false;
                Debug.Log("Got no tile");
                continue;
            }
            Debug.Log("Got a tile");
            byte[] messageBuffer = new byte[12+audioSize];
            float[] audioBuffer = new float[audioSize / sizeof(float)];
            IntPtr decoderPtr = IntPtr.Zero;
            //int descriptionFrameNr = WebRTCInvoker.get_tile_frame_number(ClientID, descriptionID);
            unsafe
            {
                fixed (byte* ptr = messageBuffer)
                {
                    WebRTCInvoker.retrieve_audio(ptr, (uint)audioSize, clientID);
                    Debug.Log("audio received");
                    pb.StartPlayback();
                    pb.DecodeAndCopyToBuffer(messageBuffer);
                    /* UInt64 timestamp = BitConverter.ToUInt64(messageBuffer, 0); ;
                     uint audioFrameNr = BitConverter.ToUInt32(messageBuffer, 8);
                     pb.CopyToBuffer(timestamp, audioFrameNr, audioBuffer);*/
                    // queues[(int)descriptionID].Enqueue(new DecodedPointCloudData(points, colors));
                }
            }
        }
    }
}
