using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[PipelineLocalRegister("spectator")]
public class PipelineLocalSpectator : PipelineLocalBase
{
    protected override string NAME => "PipelineLocalSpectator";
}
