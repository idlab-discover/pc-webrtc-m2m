using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DecodedPointCloudSingle 
{
    protected readonly object _lock = new ();
    protected readonly DecodedPointCloudMulti parent;
    public uint CapturerID;
    public uint FrameNr;
    public uint NPoints;
    public bool IsCompleted { get; protected set; }

    public DecodedPointCloudSingle(DecodedPointCloudMulti parent, uint capturerID, uint frameNr, uint nPoints)
    {
        this.parent = parent;
        CapturerID = capturerID;
        FrameNr = frameNr;
        NPoints = nPoints;
    }


}
