using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public struct PositionRecord
{
    public long timeSinceStartOfRecording;
    [JsonConverter(typeof(Vector3Serializer))]
    public Vector3 cameraPosition;
    [JsonConverter(typeof(QuaternionSerializer))]
    public Quaternion cameraRotation;
    [JsonConverter(typeof(Vector3Serializer))]
    public Vector3 objectPosition;
    [JsonConverter(typeof(QuaternionSerializer))]
    public Quaternion objectRotation;
}
