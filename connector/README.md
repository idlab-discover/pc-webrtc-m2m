# L4S Proxy Unity Plugin

This repository contains the Unity plugin that can be used to communicate with the L4S WSL2 client. You can build the plugin with Visual Studio (make sure you build the release version and not the debug one) and copy the .dll file to your Unity application.

> Note by Jack: there's now also a `Makefile` to build the plugin on Mac (tested) and Linux (untested).

If you are using WSL and you want to know what ip to use check: https://github.ugent.be/madfr/l4s-proxy-windows-testapp

## C# Examples

### Receive Example

https://github.ugent.be/madfr/PluginReceiveExample

### Send Example

https://github.ugent.be/madfr/PluginSendExample

## Changes to Old Version
private static extern int setup_connection(string ip) => private static extern int setup_connection(string ip, UInt32 port)

private static extern void set_data(byte[] points) => private static extern void set_frame_data(byte[] points)

### Additions

private static extern int send_frame_data(byte[] data, uint size)

private static extern int send_control_data(byte[] data, uint size)

## Using it in Unity
First you will need to place the built .dll file in a place where Unity can find it. Ideally this should be a Plugin folder in the Assets folder. 

Now whenever you have a class that needs to use the plugin you will need to define the functions of the plugin that you want to use like follows:

```csharp
  [DllImport("L4SProxyPlugin")]
  private static extern int setup_connection(string ip, UInt32 port);
  [DllImport("L4SProxyPlugin")]
  private static extern void start_listening();
  [DllImport("L4SProxyPlugin")]
  private static extern int next_frame();
  [DllImport("L4SProxyPlugin")]
  private static extern int next_control_packet();
  [DllImport("L4SProxyPlugin")]
  // You should technically be able to pass any type of pointer (array) to the plugin, however this has not yet been tested
  // This means that you should be able to pass an array of structures, i.e. points, and that the array should fill itself
  // And that you don't need to do any parsing in Unity (however, not yet tested)
  private static extern void set_frame_data(byte[] data);
  [DllImport("L4SProxyPlugin")]
  private static extern void set_control_data(byte[] data);
  [DllImport("L4SProxyPlugin")]
  private static extern int send_frame_data(byte[] data, uint size)
  [DllImport("L4SProxyPlugin")]
  private static extern int send_control_data(byte[] data, uint size)
  [DllImport("L4SProxyPlugin")]
  private static extern void clean_up();
```

First you should call the setup_connection(string ip) and start_listening() whenever you want to begin listening to L4S client, this could be in the Start() function or whenever a condition is fullfilled. When closing the application it is best to clean up any resources and make sure sockets are closed, this can be done by calling the clean_up() function. Unity has a function that is called whenever the application is closed which you can use as follows:

```csharp
void OnApplicationQuit()
{
  clean_up();
}
```

You yourself are responsible for parsing the raw data returned from the plugin. You can pass any pointer to the set_data() function as this function just simply copies data from an internal buffer to your (array) pointer.

## Editing the Plugin (only use when developing the plugin, otherwise just use the above code)
You should know that once a plugin is loaded by Unity it is never unloaded unless the editor (or application) is closed. So if you want to make changes to the plugin you will need to close Unity for them to take effect. There is also an advanced method which allows you to reload plugins, if you want to do this use the Native.cs file from the Unity test application and work as follows:

```csharp
delegate int setup_connection(string server_str, UInt32 port);
delegate void start_listening();
delegate int next_frame();
delegate void clean_up();
delegate int set_frame_data(byte[] data);
static IntPtr nativeLibraryPtr;
private MeshFilter meshFilter;
void Awake()
{
    if (nativeLibraryPtr != IntPtr.Zero) return;
    nativeLibraryPtr = Native.LoadLibrary("L4SProxyPlugin");
    if (nativeLibraryPtr == IntPtr.Zero)
    {
        Debug.LogError("Failed to load native library");
    }
}
// Start is called before the first frame update
void Start()
{
    Debug.Log(Native.Invoke<int, setup_connection>(nativeLibraryPtr, "172.22.107.250", 8000));
    Native.Invoke<start_listening>(nativeLibraryPtr);
}

// Update is called once per frame
void Update()
{

    int num = Native.Invoke<int, next_frame>(nativeLibraryPtr);
    if (num > 0)
    {
        Native.Invoke<set_frame_data>(nativeLibraryPtr, data);
        // ... Processing code
    }
}
void OnApplicationQuit()
{
    Native.Invoke<clean_up>(nativeLibraryPtr);
    if (nativeLibraryPtr == IntPtr.Zero) return;

    Debug.Log(Native.FreeLibrary(nativeLibraryPtr)
                  ? "Native library successfully unloaded."
                  : "Native library could not be unloaded.");
}
```

If you do it this way make sure you place the .dll in the root of your project and not in the Plugin folder.

## Updating the server PanZoom
If you also want to send the 6DOF of the HMD to the server you will need to do some additional work.

You will also need to define the following function:

```csharp
[DllImport("L4SProxyPlugin")]
private static extern int send_control_data(byte[] data, uint size);
```

Then you are able to use it by doing the following:

```csharp
byte[] data = new byte[28];
Array.Copy(BitConverter.GetBytes(x), 0, data, 0, 4);
Array.Copy(BitConverter.GetBytes(y), 0, data, 4, 4);
Array.Copy(BitConverter.GetBytes(rotation), 0, data, 8, 4);
Array.Copy(BitConverter.GetBytes(z), 0, data, 12, 4);
// Position
Array.Copy(BitConverter.GetBytes(x_pos), 0, data, 16, 4);
Array.Copy(BitConverter.GetBytes(y_pos), 0, data, 20, 4);
Array.Copy(BitConverter.GetBytes(z_pos), 0, data, 24, 4);
send_data_to_server(data, 28);
```
However you can also implement this you own way as the plugin just expects a data structure and memcopies this to a buffer anyway.
