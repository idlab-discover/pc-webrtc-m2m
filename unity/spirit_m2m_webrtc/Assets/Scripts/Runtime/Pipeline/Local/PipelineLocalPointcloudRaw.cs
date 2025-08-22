using AOT;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[PipelineLocalRegister("raw")]
public class PipelineLocalPointcloudRaw : PipelineLocalPointcloudBase
{
    protected override string NAME => "PipelineLocalPointcloudRaw";
    private MDCEncodingQueue encodingQueue;

    protected override FrameMode FrameMode => FrameMode.RealData;

    

    protected override void pollFramesInternal()
    {
        
    }

    void Start()
    {
        //DracoInvoker.register_description_done_callback(OnDescriptionDoneCallback); 
        //DracoInvoker.register_free_pc_callback(OnFreePCCallback);
       // encodingQueue = new MDCEncodingQueue(SessionInfo.Instance);
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
