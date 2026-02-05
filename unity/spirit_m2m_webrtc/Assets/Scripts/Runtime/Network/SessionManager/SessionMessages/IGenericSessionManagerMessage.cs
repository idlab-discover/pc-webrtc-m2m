using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class GenericSessionManagerMessage
{
    public string messageType;
    public JObject message;

    public static GenericSessionManagerMessage CreateFromJSON(string json)
    {
        return JsonConvert.DeserializeObject<GenericSessionManagerMessage>(json);
    }
}
