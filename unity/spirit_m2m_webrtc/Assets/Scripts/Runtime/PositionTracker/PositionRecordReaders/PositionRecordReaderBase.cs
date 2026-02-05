using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class PositionRecordReaderBase
{
    protected abstract string NAME { get; }
    protected abstract string READER_TYPE { get; }
    protected string fileName;
    public PositionRecordReaderBase(string _fileName)
    {
        fileName = _fileName;
    }
    public abstract PositionRecord ReadRecord();
    public abstract List<PositionRecord> ReadRecords();
    public abstract void CloseReader();
}
