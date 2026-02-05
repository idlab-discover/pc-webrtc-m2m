using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class PositionRecordWriterJSON : PositionRecordWriterBase
{
    protected override string NAME => "PositionRecordWriterJSON";

    protected override string WRITER_TYPE => "json";

    public PositionRecordWriterJSON(string fileName) : base(fileName)
    {
        // for json we just open the file at the end
    }

    public override void CloseWriter()
    {
        
    }

    public override void WriteRecord(ref PositionRecord record)
    {
        throw new System.NotImplementedException();
    }

    public override void WriteRecords(List<PositionRecord> records)
    {
        string json = JsonConvert.SerializeObject(records);
      //  File.WriteAllText(fileName, $"{WRITER_TYPE}\n");
        File.WriteAllText(fileName, json);
    }
}
