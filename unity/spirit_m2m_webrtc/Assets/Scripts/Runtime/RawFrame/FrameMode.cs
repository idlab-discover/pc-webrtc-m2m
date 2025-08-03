using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

public enum FrameMode : uint
{
    RealData = 0,
    RawData = 1,
    Both = 2,

}
public static class FrameModeExtensions
{
    public static string GetStringVersion(this FrameMode frame)
    {
        switch(frame)
        {
            case FrameMode.RealData:
                {
                    return "pointcloud";
                }
            case FrameMode.RawData:
                {
                    return "raw";
                }
            default:
                {
                    return "";
                }
        }
    }
}


