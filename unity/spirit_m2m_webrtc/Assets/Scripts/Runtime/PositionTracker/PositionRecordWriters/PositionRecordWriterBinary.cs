using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PositionRecordWriterBinary : PositionRecordWriterBase
{
    protected override string NAME => "PositionRecordWriterBinary";

    protected override string WRITER_TYPE => "binary";

    private FileStream fileStream;
    private BinaryWriter binaryWriter;

    public PositionRecordWriterBinary(string fileName) : base(fileName)
    {
        fileStream = new FileStream(fileName, FileMode.Create, FileAccess.Write);
        binaryWriter = new BinaryWriter(fileStream);
        //binaryWriter.Write($"{WRITER_TYPE}\n");
    }

    public override void WriteRecord(ref PositionRecord record)
    {
        // Timestamp
        binaryWriter.Write(record.timeSinceStartOfRecording);
        
        // ---------- Camera info -----------------
        binaryWriter.Write(record.cameraPosition.x);
        binaryWriter.Write(record.cameraPosition.y);
        binaryWriter.Write(record.cameraPosition.z);
        
        binaryWriter.Write(record.cameraRotation.x);
        binaryWriter.Write(record.cameraRotation.y);
        binaryWriter.Write(record.cameraRotation.z);
        binaryWriter.Write(record.cameraRotation.w);

        // ---------- Object info -----------------
        binaryWriter.Write(record.objectPosition.x);
        binaryWriter.Write(record.objectPosition.y);
        binaryWriter.Write(record.objectPosition.z);

        binaryWriter.Write(record.objectRotation.x);
        binaryWriter.Write(record.objectRotation.y);
        binaryWriter.Write(record.objectRotation.z);
        binaryWriter.Write(record.objectRotation.w);

    }

    public override void WriteRecords(List<PositionRecord> records)
    {
        foreach(var record in records)
        {
            var recordCpy = record;
            WriteRecord(ref recordCpy);
        }
    }

    public override void CloseWriter() { 
        binaryWriter.Close();
        fileStream.Close();
    }


}
