using Newtonsoft.Json;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using System.IO;
/*
* {
*   providers: [
*      {
*          key:    XXX
*          type:   XXX
*          providerSettings:   (as json)
*          sendingTracks:  [
*              {
*                  capturerID:     XXX
*                  descriptionID:  XXX
*              },
*              ...
*          ]
*      },
*      ...
*   ]
*   clients: [
*      {
*          clientID:   XXX,
*          clientSettings: (as json){
*              nDescriptions:  XXX
*              nCapturers:     XXX
*          }
*          receivingTracks:    [
*              {
*                  providerKey:    XXX
*                  capturerID:     XXX
*                  descriptionID:  XXX
*                  trackSettings:  XXX
*              }
*          ]
*      },
*      ...
*   ]
* }
*/
[System.Serializable]
public class SessionConnectionMessage
{
    public string defaultProvider;
    public string codecMode;
    public List<ConnectionProviderMessage> providers = new();
    public List<ConnectedClientMessage> clients = new();

    public static SessionConnectionMessage CreateFromJSON(string data)
    {
        return JsonConvert.DeserializeObject<SessionConnectionMessage>(data);
    }
    public string ConvertToJSON()
    {
        return JsonConvert.SerializeObject(this);
    }
}
