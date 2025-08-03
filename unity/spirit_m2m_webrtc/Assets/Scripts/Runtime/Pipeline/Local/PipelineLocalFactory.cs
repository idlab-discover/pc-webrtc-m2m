using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PipelineLocalFactory
{
    private static readonly AttributedFactory<PipelineLocalRegisterAttribute, PipelineLocalBase> instance = new();

    public static PipelineLocalBase CreatePipelineLocal(string type, params object[] args)
    {
        return instance.Create(type, args);
    }
}
