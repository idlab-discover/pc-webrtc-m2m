using Newtonsoft.Json;
using System;
using UnityEngine;
public class Vector3Serializer : JsonConverter<Vector3>
{
    public override void WriteJson(JsonWriter writer, Vector3 value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.x);
        writer.WriteValue(value.y);
        writer.WriteValue(value.z);
        writer.WriteEndArray();
    }

    public override Vector3 ReadJson(JsonReader reader, Type objectType, Vector3 existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        float[] coords = serializer.Deserialize<float[]>(reader);
        return (coords != null && coords.Length == 3) 
            ? new Vector3(coords[0], coords[1], coords[2]) 
            : Vector3.zero;
    }
}