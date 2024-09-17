using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public class DecodedPointCloudData
{
    public int FrameNr;
    public int NPoints;
    public int MaxDescriptions;
    public int CurrentNDescriptions;
    public int Quality = 0;
    public List<Vector3> Points;
    public List<Color32> Colors;
    public List<bool> CompletionStatus;
    private Mutex mut = new Mutex();
    public DecodedPointCloudData(int frameNr, int nPoints, int maxDescriptions, List<bool> activeDescriptions)
    {
        FrameNr = frameNr;
        NPoints = nPoints;
        Points = new(nPoints);
        Colors = new(nPoints);
        MaxDescriptions = maxDescriptions;
        CompletionStatus = new(maxDescriptions);
        for(int i = 0; i < activeDescriptions.Count; i++)
        {
            CompletionStatus.Add(!activeDescriptions[i]);
        }
    }
    public void LockClass() { 
        mut.WaitOne();
    }
    public void UnlockClass()
    {
        mut.ReleaseMutex();
    }
    public bool IsCompleted { get { return CompletionStatus.All(c => c); } }
}
