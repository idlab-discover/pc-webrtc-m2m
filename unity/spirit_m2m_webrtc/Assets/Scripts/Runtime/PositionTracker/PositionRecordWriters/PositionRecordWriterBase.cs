using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public abstract class PositionRecordWriterBase
{
    protected abstract string NAME { get; }
    protected abstract string WRITER_TYPE { get; }
    protected string fileName;
    public PositionRecordWriterBase(string _fileName)
    {
        fileName = _fileName;
    }
    public abstract void WriteRecord(ref PositionRecord record);
    public abstract void WriteRecords(List<PositionRecord> records);
    public abstract void CloseWriter();
}
