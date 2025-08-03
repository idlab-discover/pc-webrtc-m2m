using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor.PackageManager;
using UnityEngine;

public class LoopbackSender : NetworkSenderBase
{
    private readonly LoopbackProvider provider;
    public LoopbackSender(string providerID)
    {
        ConnectionProviderBase c = ConnectionProviderRepository.GetProvider(providerID);
        if (c == null)
        {
            // Error logging
            IsValid = false;
            return;
        }
        provider = (LoopbackProvider)c;
        IsValid = true;
    }
    public override int SendAudioData(IntPtr data, uint size)
    {
        provider.SendAudioToAllClients(data, size);
        return (int)size;
    }

    public override int SendVideoData(IntPtr data, uint size, uint capturerID, uint descriptionID)
    {
        provider.SendVideoToAllClients(data, size, capturerID, descriptionID);
        return (int)size;
    }

    protected override void disposeInternal()
    {
        
    }
}
