using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IMetricWriter
{
    void Init(JObject config);
    void WriteMetrics(Dictionary<uint, List<GenericMetric>> metrics, uint totalBinarySize);
}
