using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class PipelineRemoteFactory
{
    private static readonly AttributedFactory<PipelineRemoteRegisterAttribute, PipelineRemoteBase> instance = new();

    public static PipelineRemoteBase CreatePipelineRemote(string type, params object[] args)
    {
        return instance.Create(type, args);
    }
}
