using System;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using UnityEngine;

public class CompositeMetricWithValue<T> : GenericMetric where T : struct
{
    private T Value;
    private static int length = 0;
    public override int Length { get { return base.Length + length; } }
    private delegate int SpanSerializer(T instance, Span<byte> span, int offset);

    private static readonly Dictionary<string, SpanSerializer> fieldSerializers = new();
    private static readonly List<string> availableHeaders = new();
    public static List<string> ServerDefinedOrder { get; set; }
    public static byte[] Header { get; private set; }

    static CompositeMetricWithValue()
    {
        Header = new byte[0];
        var fields = typeof(T).GetFields(BindingFlags.Public | BindingFlags.Instance);

        foreach (var field in fields)
        {
            string name = field.Name;
            availableHeaders.Add(name);

            // Params: (T instance, Span<byte> span, int offset)
            var instanceParam = Expression.Parameter(typeof(T), "instance");
            var spanParam = Expression.Parameter(typeof(Span<byte>), "span");
            var offsetParam = Expression.Parameter(typeof(int), "offset");

            // Access: instance.FieldName
            var fieldAccess = Expression.Field(instanceParam, field);

            // Target: ByteConverterCache<FieldType>.Converter.CopyToByteArray(span, offset, value)
            var converterType = typeof(ByteConverterCache<>).MakeGenericType(field.FieldType);
            Debug.Log("FIELD" +field.FieldType + " " + converterType);
            var converterField = converterType.GetProperty("Converter", BindingFlags.Static | BindingFlags.Public);
            var converterInstance = converterField.GetValue(null);
            var sizeProperty = converterField.PropertyType.GetProperty("ValueSize");
            if (sizeProperty != null)
            {
                int fieldSize = (int)sizeProperty.GetValue(converterInstance);
                length += fieldSize;
            }
            var methodInfoHeader = converterType.GetMethod("GetHeader", BindingFlags.Public | BindingFlags.Static);
            byte[] typeHeader = (byte[])methodInfoHeader.Invoke(null, null);
            byte[] nameBuffer = Encoding.UTF8.GetBytes(name);
            uint nameLength = (uint)nameBuffer.Length;

            // Convert nameLength (int) into a byte[].
            // Using BitConverter.GetBytes produces a 4-byte array representing the int.
            // If you need a specific endianness use BinaryPrimitives / IPAddress methods.
            byte[] nameLengthBuffer = BitConverter.GetBytes(nameLength);

            Debug.Log("TYPE TEST " + typeHeader.Length);
            // Append length + name + type header to the running Header buffer
            Header = Header.Concat(nameLengthBuffer).Concat(nameBuffer).Concat(typeHeader).ToArray();

            // Note: Replace "CopyToByteArray" with the exact method name in your ByteConverterCache
            var methodInfo = converterField.PropertyType.GetMethod("CopyToByteArray");

            var call = Expression.Call(
                Expression.Property(null, converterField),
                methodInfo,
                spanParam, offsetParam, fieldAccess
            );

            var lambda = Expression.Lambda<SpanSerializer>(call, instanceParam, spanParam, offsetParam).Compile();
            fieldSerializers[name] = lambda;
        }
    }
    public CompositeMetricWithValue(T value) : base()
    {
        Value = value;
    }
    public override int CopyToBuffer(Span<byte> byteArray)
    {
        {
            // 1. Get starting offset from base
            int currentOffset = base.CopyToBuffer(byteArray);

            // 2. Loop through fields in the order specified
            foreach(var ser in fieldSerializers.Values)
            {
                currentOffset += ser(Value, byteArray.Slice(currentOffset), currentOffset);
            }
            /*foreach (var fieldName in ServerDefinedOrder)
            {
                if (fieldSerializers.TryGetValue(fieldName, out var serialize))
                {
                    // Each call invokes the specific ByteConverterCache<K> for that field type
                    currentOffset += serialize(Value, byteArray, currentOffset);
                }
            }*/

            return currentOffset;
        }
    }
}
