using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MetricWriterASCII : IMetricWriter
{
    public void Init(JObject config)
    {
        throw new System.NotImplementedException();
    }

    public void WriteMetrics(Dictionary<uint, List<GenericMetric>> metrics, uint totalBinarySize)
    {
        throw new System.NotImplementedException();
    }
}
