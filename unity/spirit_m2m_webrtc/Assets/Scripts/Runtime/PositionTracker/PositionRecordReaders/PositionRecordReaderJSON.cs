using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PositionRecordReaderJSON : PositionRecordReaderBase
{
    protected override string NAME => "PositionRecordReaderJSON";

    protected override string READER_TYPE => "json";

    public PositionRecordReaderJSON(string filePath) : base(filePath)
    {

    }

    public override void CloseReader()
    {
        
    }

    public override PositionRecord ReadRecord()
    {
        throw new System.NotImplementedException();
    }

    public override List<PositionRecord> ReadRecords()
    {
        string json = System.IO.File.ReadAllText(fileName);
        return JsonConvert.DeserializeObject<List<PositionRecord>>(json);
    }
}
