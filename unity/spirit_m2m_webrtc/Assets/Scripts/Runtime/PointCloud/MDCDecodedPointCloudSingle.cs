using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

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
            Parent.Quality = (uint)Math.Ceiling(((float)Parent.ActualPoints / NPoints) *100); // TODO Update this for multiple singles
            Parent.Quality = Math.Min(Parent.Quality, 100); // Just in case
            if(currentNDescriptions == nDescriptions)
            {
                IsCompleted = true;
            }
           
        }
    }
 
}
