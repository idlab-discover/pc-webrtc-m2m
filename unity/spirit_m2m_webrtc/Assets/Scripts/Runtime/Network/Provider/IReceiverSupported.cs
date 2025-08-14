using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface IReceiverSupported
{
    public NetworkReceiverBase GetReceiver(ReceivingTrackInfo track, uint clientID);
}
