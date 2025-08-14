using System;
using System.Collections;
using System.Collections.Generic;
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

    public override int SendVideoData(string trackID, IntPtr data, uint size)
    {
        provider.SendVideoToAllClients(trackID, data, size);
        return (int)size;
    }

    protected override void disposeInternal()
    {
        
    }
}
