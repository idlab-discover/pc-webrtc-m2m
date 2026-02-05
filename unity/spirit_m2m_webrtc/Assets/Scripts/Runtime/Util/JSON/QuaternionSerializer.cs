using Newtonsoft.Json;
using System;
using UnityEngine;
public class QuaternionSerializer : JsonConverter<Quaternion>
{
    public override void WriteJson(JsonWriter writer, Quaternion value, JsonSerializer serializer)
    {
        writer.WriteStartArray();
        writer.WriteValue(value.x);
        writer.WriteValue(value.y);
        writer.WriteValue(value.z);
        writer.WriteValue(value.w);
        writer.WriteEndArray();
    }

    public override Quaternion ReadJson(JsonReader reader, Type objectType, Quaternion existingValue, bool hasExistingValue, JsonSerializer serializer)
    {
        float[] coords = serializer.Deserialize<float[]>(reader);
        return (coords != null && coords.Length == 4)
            ? new Quaternion(coords[0], coords[1], coords[2], coords[3])
            : Quaternion.identity;
    }
}