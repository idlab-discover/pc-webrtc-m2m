using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Xml;

using UnityEngine;
using UnityEngine.Rendering;

public class PCReceiver : MonoBehaviour
{
    public uint ClientID;
    public int NDescriptions;
    private List<bool> activeDescriptions = new List<bool> { false, false, false };
    private List<System.Threading.Thread> workerThreads = new List<System.Threading.Thread>();
  //  private List<ConcurrentQueue<DecodedPointCloudData>> queues = new List<ConcurrentQueue<DecodedPointCloudData>>();

    private Dictionary<int, DecodedPointCloudData> inProgessFrames;
    private ConcurrentQueue<DecodedPointCloudData> queue;
    private static Mutex mut = new Mutex();
    private int lastCompletedFrameNr = -1;

    private bool keep_working = true;

    // ####################### Unity GameObjects #########################
    public List<GameObject> PCRenderers;
    Mesh currentMesh;
    private List<MeshFilter> meshFilters;

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
       // activeDescriptions = new(NDescriptions);
        for(int i = 0; i < NDescriptions; i++)
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
                
            }
        }
       
    }
    void OnDestroy()
    {
        for(int i = 0;i < NDescriptions;i++)
        {
       //     workerThreads[i].Join();
        }    
    }
    private void pollDescription(uint descriptionID)
    {
        WebRTCInvoker.wait_for_peer();
        while (keep_working)
        {
            Debug.Log("Polling size");
            int descriptionSize = WebRTCInvoker.get_tile_size(ClientID, descriptionID);
            
            if (descriptionSize == 0)
            {
                keep_working = false;
                Debug.Log("Got no tile");
                continue;
            }
            Debug.Log("Got a tile");
            byte[] messageBuffer = new byte[descriptionSize];
            IntPtr decoderPtr = IntPtr.Zero;
            //int descriptionFrameNr = WebRTCInvoker.get_tile_frame_number(ClientID, descriptionID);
            unsafe
            {
                fixed (byte* ptr = messageBuffer)
                {
                    WebRTCInvoker.retrieve_tile(ptr, (uint)descriptionSize, ClientID, descriptionID);
                    int descriptionFrameNr = BitConverter.ToInt32(messageBuffer, 0);
                    if(descriptionFrameNr <= lastCompletedFrameNr)
                    {
                        continue;
                    }
                    Debug.Log($"Start decoding");
                    decoderPtr = DracoInvoker.decode_pc(ptr + 8, (uint)descriptionSize);
                    Debug.Log($"Decoding done");           
                    if (decoderPtr == IntPtr.Zero)
                    {
                        Debug.Log($"Debug error at client {ClientID} for description {descriptionID}");
                        continue;
                    }
                    mut.WaitOne();
                    DecodedPointCloudData pcData;
                    if (!inProgessFrames.TryGetValue(descriptionFrameNr, out pcData))
                    {
                        int nTotalPointsInFrame = BitConverter.ToInt32(messageBuffer, 4);
                        pcData = new DecodedPointCloudData(descriptionFrameNr, nTotalPointsInFrame, NDescriptions, activeDescriptions);
                        inProgessFrames.Add(descriptionFrameNr, pcData);
                    }
                    UInt32 nDecodedPoints = DracoInvoker.get_n_points(decoderPtr);
                    // TODO
                    //      * Get header from frame
                    //      * Check if frame exists
                    //      * If not 

                    // Header:
                    //          * FrameNr
                    //          * NPointsFrame
                    //          * DescriptionNr

                    // Frame:
                    //          * NCompleted
                    //          * Quality

                    // TODO check if frame exists
                    IntPtr pointsPtr = DracoInvoker.get_point_array(decoderPtr);
                    IntPtr colorPtr = DracoInvoker.get_color_array(decoderPtr);
                    float* pointsUnsafePtr = (float*)pointsPtr;
                    byte* colorsUnsafePtr = (byte*)colorPtr;

                    for (int i = 0; i < nDecodedPoints; i++)
                    {
                        //    points[i] = new Vector3(0, 0, 0);
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
                        if(descriptionFrameNr % 10 == 0)
                        {
                            Debug.Log($"Frame {descriptionFrameNr} completed, last compl= {lastCompletedFrameNr}");
                        }
                       
                        inProgessFrames.Remove(descriptionFrameNr);
                        if (descriptionFrameNr >= lastCompletedFrameNr)
                        {
                            lastCompletedFrameNr = pcData.FrameNr;
                            queue.Enqueue(pcData);
                        }
                        
                    }
                    mut.ReleaseMutex();
                // queues[(int)descriptionID].Enqueue(new DecodedPointCloudData(points, colors));
                }
            }              
        }
    }
    public void OnTrackChange(uint frameNr, int descriptionID, bool isAdded)
    {
        mut.WaitOne();
        activeDescriptions[descriptionID] = isAdded;
        if (isAdded)
        {

        } else
        {
            List<int> toRemove = new();
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
                            lastCompletedFrameNr = fr.Value.FrameNr;
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
}
