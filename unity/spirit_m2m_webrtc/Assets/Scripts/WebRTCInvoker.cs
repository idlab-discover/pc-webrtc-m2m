using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static DLLLogger;

public unsafe class WebRTCInvoker
{
    [DllImport("WebRTCConnector")]
    public static extern void set_logging(string log_directory, int logLevel);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterDebugCallback(DLLLogger.debugCallback cb);
    [DllImport("WebRTCConnector")]
    public static extern int initialize(string ip_send, UInt32 port_send, string ip_recv, UInt32 port_recv,
        UInt32 number_of_tiles, UInt32 client_id, string api_version);
    [DllImport("WebRTCConnector")]
    public static extern void clean_up();
    [DllImport("WebRTCConnector")]
    public static extern int send_tile(byte* data, UInt32 size, UInt32 tile_number);
    [DllImport("WebRTCConnector")]
    public static extern int get_tile_size(UInt32 client_id, UInt32 tile_number);
    [DllImport("WebRTCConnector")]
    public static extern int get_tile_frame_number(UInt32 client_id, UInt32 tile_number);
    [DllImport("WebRTCConnector")]
    public static extern void retrieve_tile(byte* buffer, UInt32 size, UInt32 client_id, UInt32 tile_number);

    // Audio frame functions
    [DllImport("WebRTCConnector")]
    public static extern int send_audio(IntPtr data, UInt32 size);
    [DllImport("WebRTCConnector")]
    public static extern int get_audio_size(UInt32 client_id);
    [DllImport("WebRTCConnector")]
    public static extern void retrieve_audio(IntPtr buffer, UInt32 size, UInt32 client_id);

    [DllImport("WebRTCConnector")]
    public static extern int send_control(IntPtr data, UInt32 size);
    [DllImport("WebRTCConnector")]
    public static extern int get_control_size();
    [DllImport("WebRTCConnector")]
    public static extern void retrieve_control(IntPtr buffer);
    [DllImport("WebRTCConnector")]
    public static extern void wait_for_peer();

    // Control packet functions
    [DllImport("WebRTCConnector")]
    public static extern int send_control_packet(byte* data, UInt32 size);

    public delegate void trackChangeCb(UInt32 clientID, UInt32 lastFrameNr, UInt32 tileNr, bool isAdded);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_track_change_callback(trackChangeCb cb);
}
