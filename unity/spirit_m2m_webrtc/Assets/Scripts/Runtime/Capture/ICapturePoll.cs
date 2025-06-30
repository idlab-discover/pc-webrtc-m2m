using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface ICapturePoll 
{
    public IntPtr PollNextRawFrame();
    public IntPtr PollNextPointCloud();
    public IntPtr PollNextFrame();
}
