
using FMODUnity;
using System;
using System.Runtime.InteropServices;
using Unity.Burst.CompilerServices;

public enum ColorCodecType
{
    Jpeg = 0,
    WebP = 1,
    Invalid = 9999,
}

public class ColorCodecSettings : IDisposable
{
    public ColorCodecType CodecType { get; private set; }
    public IntPtr SettingsPtr { get { return handle.AddrOfPinnedObject(); } }
    private bool disposedValue;
    private GCHandle handle;

    public ColorCodecSettings(ColorCodecType codecType, GCHandle handle)
    {
        this.CodecType=codecType; 
        this.handle = handle;
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            if (disposing)
            {
                if(handle != null)
                {
                    handle.Free();
                }
               
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

public class ColorCodecHelper
{
    public static ColorCodecSettings GetCodecSettings(RawEncodingSettings settings)
    {
        ColorCodecType cType = GetTypeForString(settings.colorCodecName);
        switch (cType)
        {
            case ColorCodecType.Jpeg: {
                    JPEGSettingsEx jpegSettings = new() { quality = settings.jpegSettings.quality };
                    return new ColorCodecSettings(cType, GCHandle.Alloc(jpegSettings, GCHandleType.Pinned));
                }
            case ColorCodecType.WebP: {
                    WebPSettingsEx webPSettings = new() { quality = settings.webPSettings.quality,  method = settings.webPSettings.method};
                    return new ColorCodecSettings(cType, GCHandle.Alloc(webPSettings, GCHandleType.Pinned));
                }
            default: {
                    return null;
                }
        }
    }
    public static ColorCodecType GetTypeForString(string type)
    {
        switch(type.ToLower())
        {
            case "jpeg": {
                    return ColorCodecType.Jpeg;
                }
            case "webp": {
                    return ColorCodecType.WebP;
                }
            default: {
                    return ColorCodecType.Invalid;
                }
        }
    }
}