
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;

public enum DepthCodecType
{
    VLE = 0,
    ZDepth = 1,
    Invalid = 9999,
}

public class DepthCodecSettings : IDisposable
{
    public DepthCodecType CodecType { get; private set; }
    public IntPtr SettingsPtr { get { return IntPtr.Zero; } }
    private bool disposedValue;
  
    public DepthCodecSettings(DepthCodecType codecType)
    {
        this.CodecType = codecType;
       
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
             
            }


            disposedValue = true;
        }
    }

    public void Dispose()
    {
        Dispose(disposing: true);
        GC.SuppressFinalize(this);
    }
}

public class DepthCodecHelper
{
    public static DepthCodecSettings GetCodecSettings(RawEncodingSettings settings)
    {
        DepthCodecType cType = GetTypeForString(settings.depthCodecName);
        switch (cType)
        {
            case DepthCodecType.VLE:
                {
                    return new DepthCodecSettings(cType);
                }
            case DepthCodecType.ZDepth:
                {
                    return new DepthCodecSettings(cType);
                }
            default:
                {
                    return null;
                }
        }
    }
    public static DepthCodecType GetTypeForString(string type)
    {
        switch (type.ToLower())
        {
            case "vle":
                {
                    return DepthCodecType.VLE;
                }
            case "zdepth":
                {
                    return DepthCodecType.ZDepth;
                }
            default:
                {
                    return DepthCodecType.Invalid;
                }
        }
    }
}