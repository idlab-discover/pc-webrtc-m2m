using System.Collections;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEngine;
using System.Diagnostics;
using System;

public struct SystemUsageMetric
{
    public int CpuUsagePercent;
    public int MemoryUsageMb;
}

public class SystemUsage : MonoBehaviour
{
    private ProfilerRecorder systemMemoryRecorder;
    private Process proc;
    public float UsageUpdateTime = 0.100f;
    private float currentUsageUpdateTimer = 0;
    TimeSpan prevCPU = TimeSpan.MinValue;
    DateTime prevTime = DateTime.Now;

    private CompositeMetricDefinition<SystemUsageMetric> systemUsageMetricDefinition;

    public void Init()
    {
        systemUsageMetricDefinition = MetricController.MetricServerConnection.RegisterCompositePushMetric<SystemUsageMetric>("SystemUsage");
    }
    void Start()
    {
        systemMemoryRecorder = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "System Used Memory");
        proc = Process.GetCurrentProcess();
    }

    void Update()
    {
        currentUsageUpdateTimer += Time.deltaTime;
        if (currentUsageUpdateTimer > UsageUpdateTime)
        {
            proc.Refresh();
            TimeSpan endCpu = proc.TotalProcessorTime;
            DateTime endTime = DateTime.Now;
            if (prevCPU != TimeSpan.MinValue)
            {
                double cpuUsedMs = (endCpu - prevCPU).TotalMilliseconds;
                double totalMsPassed = (endTime - prevTime).TotalMilliseconds;

                int cpuPercent = (int)(cpuUsedMs / (totalMsPassed * Environment.ProcessorCount) * 100);
                int memMb = (int)(systemMemoryRecorder.LastValue / 1024.0 / 1024.0);
                systemUsageMetricDefinition?.AddCapturedValue(new SystemUsageMetric
                {
                    CpuUsagePercent = cpuPercent,
                    MemoryUsageMb = memMb
                });
            }
            prevCPU = endCpu;
            prevTime = endTime;
            currentUsageUpdateTimer = 0;
        }
    }

}
