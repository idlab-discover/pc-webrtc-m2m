using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

public class MDCFrameHeader 
{
    public static int HEADER_SIZE = 28;
    public IntPtr DataBuffer { get; private set; }
    public uint DataBufferSize { get; private set; }
    public ulong Timestamp { get; private set; }
    public uint CapturerID { get; private set; }
    public uint FrameNr { get; private set; }
    public uint DescriptionNr { get; private set; }
    public uint CodecType { get; private set; }
    public uint TotalNumberOfPoints { get; private set; }


    public byte[] Bytes 
    { 
        get 
        {
            byte[] frameHeader = new byte[HEADER_SIZE];
            var timestampField = BitConverter.GetBytes(Timestamp);
            timestampField.CopyTo(frameHeader, 0);
            var capturerIDField = BitConverter.GetBytes(CapturerID);
            capturerIDField.CopyTo(frameHeader, 8);
            var frameNrField = BitConverter.GetBytes(FrameNr);
            frameNrField.CopyTo(frameHeader, 12);
            var descriptionNrField = BitConverter.GetBytes(DescriptionNr);
            descriptionNrField.CopyTo(frameHeader, 16);
            var codecType = BitConverter.GetBytes(CodecType);
            codecType.CopyTo(frameHeader, 20);
            var nPointsFrameField = BitConverter.GetBytes(TotalNumberOfPoints);
            nPointsFrameField.CopyTo(frameHeader, 24);
          
            return frameHeader;
        }
    }
    public MDCFrameHeader(
        IntPtr dataBuffer,
        uint dataBufferSize,
        ulong timestamp,
        uint capturerID,
        uint frameNr,
        uint descriptionNr,
        uint codecType,
        uint numberOfPoints)
    {
        DataBuffer = dataBuffer;
        DataBufferSize = dataBufferSize;
        Timestamp = timestamp;
        CapturerID = capturerID;
        FrameNr = frameNr;
        DescriptionNr = descriptionNr;
        CodecType = codecType;
        TotalNumberOfPoints = numberOfPoints;
    }
    public MDCFrameHeader(NetworkFrame frame)
    {
        copyFromBuffer(frame);
    }
    private void copyFromBuffer(NetworkFrame frame)
    {
        unsafe
        {
            DataBuffer = frame.DataPtr + HEADER_SIZE;
            DataBufferSize = frame.Size - (uint)HEADER_SIZE;
            Timestamp = (ulong)Marshal.ReadInt64(frame.DataPtr, 0);
            CapturerID = (uint)Marshal.ReadInt32(frame.DataPtr, 8);
            FrameNr = (uint)Marshal.ReadInt32(frame.DataPtr, 12);
            DescriptionNr = (uint)Marshal.ReadInt32(frame.DataPtr, 16);
            CodecType = (uint)Marshal.ReadInt32(frame.DataPtr, 20);
            TotalNumberOfPoints = (uint)Marshal.ReadInt32(frame.DataPtr, 24);
         
        }
    }
    public override string ToString()
    {
        return $"ts_frame={Timestamp} capturer_id={CapturerID} frameNr={FrameNr} descriptionNr={DescriptionNr} codecType={CodecType} totalNumberOfPoints={TotalNumberOfPoints}";
    }
    public string ToStringSmall()
    {
        return $"capturerID={CapturerID} frameNr={FrameNr} descriptionID={DescriptionNr}";
    }
}
