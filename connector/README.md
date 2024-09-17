# Connector

This repository contains the Unity plugin that can be used to connect the WebRTC client to Unity. You can build the plugin with Visual Studio (make sure you build the `release` version) and copy the .dll file to your Unity application.

## Using the plugin in Unity

First you will need to place the built .dll file in a place where Unity can find it. Ideally this is the `Plugin` folder in the `Assets` folder.

### DLL import

Whenever you have a class that needs to use the functionalities of the plugin, you will need to define the functions you want to use as follows:

```csharp
  // Logging in Unity
[DllImport("WebRTCConnector")]
public static extern void set_logging(string log_directory, int logLevel);
[DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
public static extern void RegisterDebugCallback(debugCallback cb);

// Initialization and cleanup
[DllImport("WebRTCConnector")]
public static extern int initialize(string ip_send, UInt32 port_send, string ip_recv, UInt32 port_recv, UInt32 number_of_tiles, UInt32 client_id, string api_version);
[DllImport("WebRTCConnector")]
public static extern void clean_up();

// Video data
[DllImport("WebRTCConnector")]
public static extern int send_tile(byte* data, UInt32 size, UInt32 tile_number, UInt32 quality);
[DllImport("WebRTCConnector")]
public static extern int get_tile_size(UInt32 client_id, UInt32 tile_number);
[DllImport("WebRTCConnector")]
public static extern void retrieve_tile(byte* buffer, UInt32 size, UInt32 client_id, UInt32 tile_number);

// Audio data
[DllImport("WebRTCConnector")]
public static extern int send_audio(byte* data, UInt32 size);
[DllImport("WebRTCConnector")]
public static extern int get_audio_size(UInt32 client_id);
[DllImport("WebRTCConnector")]
public static extern void retrieve_audio(byte* buffer, UInt32 size, UInt32 client_id);

// Control messages (e.g., quality decision-making)
[DllImport("WebRTCConnector")]
public static extern int send_control_packet(byte* data, UInt32 size);
```

### Initialization

In Unity, first call `RegisterDebugCallback` like this:

```csharp
[MonoPInvokeCallback(typeof(debugCallback))]
static void OnDebugCallback(IntPtr message, int console_level, int color, int size)
{
    // Ptr to string
    string debug_string;
    try {
        debug_string = Marshal.PtrToStringAnsi(message, size);
    }
    catch(ArgumentException) {
        debug_string = $"OnDebugCallback: Marshal.PtrToStringAnsi() raised an exception (string size={size})";
    }
    // Add specified color
    debug_string = $"WebRTCConnectorPinvoke: <color={((Color)color).ToString()}>{debug_string}</color>";
    // Output the message
    if (console_level == 0)
    {
        Debug.Log(debug_string);
    } else if (console_level == 1)
    {
        Debug.LogWarning(debug_string);
    } else
    {
        Debug.LogError(debug_string);
    }
}
```

This is used to allow for console log and debug messages from the connector. Then, you can use `set_logging(string log_directory, int logLevel)` to specify a log directory (if any) where logs will be stored, and a log level that indicates the degree of output:

- Default: generic setup information and potential error messages
- Verbose: additional information on the setup (limited number of messages)
- Debug: relevant information for each incoming/outgoing packet, buffer sizes, etc.

On top of this, three console levels are considered for use with [Unity](../unity):

- Log
- Warning
- Error

Now, run `initialize(string ip_send, UInt32 port_send, string ip_recv, UInt32 port_recv, UInt32 number_of_tiles, UInt32 client_id, string api_version)` to start the session. The first four arguments are related to those of the WebRTC clients, while the number of tiles and the client ID should be known in the Unity application. The last arugment is meant for versioning, makeing sure that the DLL's version and the one expected by the Unity application are the same.

### Sending and receiving video and audio

From now on, you can send and receive video and audio using the provided functions. You yourself are responsible for parsing the raw data returned from the plugin: you can pass any pointer to the `send_tile()` and `send_audio()` functions, as these functions simply copy data from an internal buffer to your (array) pointer. More details can be found [here](../unity).

### Cleaning up

 When closing the application it is best to clean up any resources and make sure sockets are closed, this can be done by calling the clean_up() function. Unity has a function that is called whenever the application is closed which you can use as follows:

```csharp
void OnApplicationQuit()
{
    clean_up();
}
```

***********************************
# TODO: update the below (outdated)
***********************************

## Editing the plugin (only use when developing the plugin, otherwise just use the above code)

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
    nativeLibraryPtr = Native.LoadLibrary("ProxyPlugin");
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
[DllImport("ProxyProxyPlugin")]
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
