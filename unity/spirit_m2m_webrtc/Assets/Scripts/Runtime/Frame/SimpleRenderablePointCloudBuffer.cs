using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

public class SimpleRenderablePointCloudBuffer : RenderablePointCloudBuffer
{
    private ConcurrentQueue<SimplePointCloud> queue = new();

    public SimpleRenderablePointCloudBuffer(uint fps) : base(new PlaybackBufferSettings(), 0, fps)
    {
        
    }
    public override RenderablePointCloud CheckForCompletedFrames()
    {
        if (!queue.IsEmpty)
        {
            bool succes = queue.TryDequeue(out SimplePointCloud dec);
            if (succes)
            {
                return dec;
            }
            return null;
        }
        return null;
    }

    public void EnqueueFrame(SimplePointCloud frame)
    {
        queue.Enqueue(frame);
    }


}
