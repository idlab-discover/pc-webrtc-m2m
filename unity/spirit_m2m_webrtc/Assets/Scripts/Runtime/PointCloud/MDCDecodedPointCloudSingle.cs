using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MDCDecodedPointCloudSingle : DecodedPointCloudSingle
{
    private readonly uint nDescriptions;
    private uint currentNDescriptions = 0;
    public MDCDecodedPointCloudSingle(DecodedPointCloudMulti parent, uint capturerID, uint frameNr, uint nPoints, uint nDescriptions) 
        : base(parent, capturerID, frameNr, nPoints)
    {
        this.nDescriptions = nDescriptions;
    }

    public void AddDescription(DecodedMDCDescription desc)
    {
        lock (_lock)
        {
            Parent.AddPoints(desc.PointPtr, desc.ColorPtr, desc.NumberOfPoints);
            currentNDescriptions++;
            if(currentNDescriptions == nDescriptions)
            {
                Parent.Quality = (uint)(((float)Parent.ActualPoints / NPoints) *100); // TODO Update this for multiple singles
                IsCompleted = true;
            }
           
        }
    }
 
}
