using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using UnityEngine;


struct MetricServerDefinitionInfo
{
    public bool IsComposite;
    public string Name;
    public byte[] Header;
    public uint MetricId;
    public int NumberOfFields;
}
public class MetricServerConnectionLocal : MetricServerConnectionBase
{
    private uint metricIdCounter = 0;
    private Dictionary<uint, MetricServerDefinitionInfo> metricInfos = new();
    private readonly object _lock = new();
    protected override uint registerCompositeMetricDefinitionWithServer<T>(string metricName, byte[] header)
    {
        lock(_lock)
        {
            MetricServerDefinitionInfo info = new MetricServerDefinitionInfo
            {
                IsComposite = true,
                Name = metricName,
                MetricId = metricIdCounter++,
                NumberOfFields = 0
            };
            byte[] newHeader = new byte[0];
            Span<byte> headerSpan = new Span<byte>(header);
            int processedBytes = 0;
            Debug.Log($"Registering Composite Metric: {metricName}, Header Length: {header.Length}");
            while (processedBytes < header.Length)
            {
                Debug.Log("ProcessedBytes: " + processedBytes + " / " + header.Length);
                uint nameLength = BinaryPrimitives.ReadUInt32LittleEndian(headerSpan.Slice(processedBytes));
                processedBytes += sizeof(int);
                string name = Encoding.UTF8.GetString(headerSpan.Slice(processedBytes, (int)nameLength));
                processedBytes += (int)nameLength;
                byte typeByte = headerSpan[processedBytes];
                ByteConverterType type = (ByteConverterType)typeByte;
                newHeader = newHeader.Concat(headerSpan.Slice(processedBytes, 5).ToArray()).ToArray();
                processedBytes += sizeof(byte) + 4;
                Debug.Log($"Field: {name}, Type: {type}");
                info.NumberOfFields++;
            }
            info.Header = newHeader;
            metricInfos[info.MetricId] = info;
            return info.MetricId;
        }
        
    }

    protected override uint registerMetricDefinitionWithServer<T>(string metricName, byte[] header)
    {
        lock(_lock)
        {
            MetricServerDefinitionInfo info = new MetricServerDefinitionInfo
            {
                Header = header,
                IsComposite = true,
                Name = metricName,
                MetricId = metricIdCounter++,
                NumberOfFields = 1
            };
            metricInfos[info.MetricId] = info;
            Debug.Log($"Registered Metric: {metricName}, MetricId: {info.MetricId}");
            return info.MetricId;
        }
        
    }

    protected override void writeMetrics(byte[] bytes)
    {
        Debug.Log($"Metrics: {bytes.Length} bytes");
        int processedBytes = 4;
        Span<byte> buffer = bytes.AsSpan(0);
        while (processedBytes < bytes.Length)
        {
            //Debug.Log("PROC: " + processedBytes + " / " + bytes.Length);
            
            uint metricId = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(processedBytes));
            processedBytes += sizeof(uint);
            int numValues = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(processedBytes));
            processedBytes += sizeof(int);
            //Debug.Log($"MetricId: {metricId}, NumValues: {numValues}");
            if (metricInfos.TryGetValue(metricId, out var info))
            {
                Debug.Log($"MetricName: {info.Name}");
            }
            else
            {
                Debug.Log("Unknown Metric");
            }
            for (int i = 0; i < numValues; i++)
            {
                if (processedBytes >= bytes.Length)
                {
                    Debug.Log("Not enough bytes for value");
                    break;
                }
                processedBytes += outputValue(info, buffer.Slice(processedBytes));
                
            }
            // }
            /*if(metricHeaders.TryGetValue(metricId, out byte[] header))
            {
                Debug.Log($"Header: {System.BitConverter.ToString(header)}");
            }
            else
            {
                Debug.Log("No header");
            }*/
            // TODO Process values
        }
    }
    private int outputValue(MetricServerDefinitionInfo info, Span<byte> buffer)
    {
        byte[] header = info.Header;
        int foundFields = 0;
        int metricLength = 0;
        int headerOffset = 0;
        long timeStamp = BinaryPrimitives.ReadInt64LittleEndian(buffer);
        metricLength += sizeof(long);
        Debug.Log("Buffer length" + buffer.Length);
        while (foundFields < info.NumberOfFields)
        {
            foundFields++;
            ByteConverterType type = (ByteConverterType)header[headerOffset];
            headerOffset += 1;
            Debug.Log("Type: " + type);
            int length = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(headerOffset));
            headerOffset += sizeof(int);
            Debug.Log($"Timestamp: {timeStamp}, Length: {length}");

            if (type == ByteConverterType.UnsignedNumeric)
            {
                if (length == sizeof(uint))
                {
                    uint value = BinaryPrimitives.ReadUInt32LittleEndian(buffer.Slice(metricLength));
                    Debug.Log($"Value: {value}");
                }
                else
                {
                    Debug.Log("Unsupported length for unsigned numeric");
                }
            }
            else if (type == ByteConverterType.SignedNumeric)
            {
                if (length == sizeof(uint))
                {
                    int value = BinaryPrimitives.ReadInt32LittleEndian(buffer.Slice(metricLength));
                    Debug.Log($"Value: {value}");
                }
                else
                {
                    Debug.Log("Unsupported length for unsigned numeric");
                }
            }
            metricLength += sizeof(int);
        }
        
        return metricLength;
    }

    protected override void connectInternal()
    {
        isConnected = true;
    }
}
