
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;

public enum DepthCodecType
{
    VLE = 0,
    ZDepth = 1,
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
        switch (settings.depthCodecName.ToLower())
        {
            case "vle":
                {
                    return new DepthCodecSettings(DepthCodecType.VLE);
                }
            case "zdepth":
                {
                    return new DepthCodecSettings(DepthCodecType.ZDepth);
                }
        }
        return null;
    }
}