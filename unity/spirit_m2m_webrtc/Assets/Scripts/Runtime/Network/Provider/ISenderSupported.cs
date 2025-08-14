using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ISenderSupported 
{
    public NetworkSenderBase GetSender(ReceivingTrackInfo track);
}
