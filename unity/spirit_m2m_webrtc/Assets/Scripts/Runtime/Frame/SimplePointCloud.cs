using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using UnityEngine;

public class SimplePointCloud : RenderablePointCloud
{
    public SimplePointCloud(IntPtr pcptr) : base(Realsense2Invoker.get_point_cloud_timestamp(pcptr), Realsense2Invoker.get_point_cloud_frame_nr(pcptr), Realsense2Invoker.get_point_cloud_size(pcptr))
    {
        Quality = 100;
        lock (_lock)
        {
            unsafe
            {
                float* pointsUnsafePtr = (float*)Realsense2Invoker.get_point_cloud_pos(pcptr);
                byte* colorsUnsafePtr = (byte*)Realsense2Invoker.get_point_cloud_col(pcptr);
                uint counter = 0;
                for (int i = 0; i < TotalPoints; i++)
                {
                    if (pointsUnsafePtr[(i * 3)] == 0 && pointsUnsafePtr[(i * 3) + 1] == 0 && pointsUnsafePtr[(i * 3) + 2] == 0)
                    {
                        continue;
                    }
                    // TODO performance testing
                    Points[counter] = new Vector3(pointsUnsafePtr[(i * 3)] * -1, pointsUnsafePtr[(i * 3) + 1] * -1, pointsUnsafePtr[(i * 3) + 2] * -1);
                    Colors[counter] = new Color32(colorsUnsafePtr[(i * 3)], colorsUnsafePtr[(i * 3) + 1], colorsUnsafePtr[(i * 3) + 2], 255);
                    counter++;
                }
            }
            Realsense2Invoker.free_point_cloud(pcptr);
        }
    }
}
