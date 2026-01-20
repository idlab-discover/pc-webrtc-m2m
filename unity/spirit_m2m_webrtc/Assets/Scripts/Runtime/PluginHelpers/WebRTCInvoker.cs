using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UIElements;
using static DLLLogger;

public unsafe class WebRTCInvoker
{
    [DllImport("WebRTCConnector")]
    public static extern void set_logging(string log_directory, int logLevel);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void RegisterDebugCallback(DLLLogger.debugCallback cb);
    [DllImport("WebRTCConnector")]
    public static extern int initialize(string ip_send, UInt32 port_send, string ip_recv, UInt32 port_recv,
        UInt32 number_of_capturers, UInt32 number_of_tiles, UInt32 client_id, string api_version);
    [DllImport("WebRTCConnector")]
    public static extern void clean_up();
    [DllImport("WebRTCConnector")]
    public static extern int send_tile(byte* data, UInt32 size, UInt32 capturerID, UInt32 tile_number);
    [DllImport("WebRTCConnector")]
    public static extern int get_tile_size(UInt32 client_id, UInt32 capturerID, UInt32 tile_number);
    [DllImport("WebRTCConnector")]
    public static extern int get_tile_frame_number(UInt32 client_id, UInt32 capturerID, UInt32 tile_number);
    [DllImport("WebRTCConnector")]
    public static extern void retrieve_tile(byte* buffer, UInt32 size, UInt32 client_id, UInt32 capturerID, UInt32 tile_number);

    // Audio frame functions
    [DllImport("WebRTCConnector")]
    public static extern int send_audio(byte* data, UInt32 size);
    [DllImport("WebRTCConnector")]
    public static extern int get_audio_size(UInt32 client_id);
    [DllImport("WebRTCConnector")]
    public static extern void retrieve_audio(byte* buffer, UInt32 size, UInt32 client_id);

    [DllImport("WebRTCConnector")]
    public static extern int send_control(IntPtr data,  UInt32 size);
    [DllImport("WebRTCConnector")]
    public static extern int get_control_size();
    [DllImport("WebRTCConnector")]
    public static extern void retrieve_control(IntPtr buffer);
    [DllImport("WebRTCConnector")]
    public static extern void wait_for_peer();

    // Control packet functions
    [DllImport("WebRTCConnector")]
    public static extern int send_control_packet(byte* data, UInt32 size);

    // Intrinsics packet functions
    [DllImport("WebRTCConnector")]
    public static extern int send_intrisics_packet(byte* data, UInt32 size);

    // Callback functions
    public delegate void trackChangeCb(UInt32 clientID, UInt32 lastFrameNr, UInt32 capturerID, UInt32 tileNr, bool isAdded);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]


    public static extern void register_track_change_callback(trackChangeCb cb);

    public delegate void intrinsicsUpdatedCb(UInt32 clientID, UInt32 capturerID, UInt32 capturerType, IntPtr data);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void register_intrisics_updated_callback(intrinsicsUpdatedCb cb);



    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr create_new_webrtc_connection(UInt32 portSelf, UInt32 portRemote);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_webrtc_connection(IntPtr ptr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void wait_for_peer_connection(IntPtr ptr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern int send_track_frame(IntPtr ptr, IntPtr data, UInt32 size, UInt32 internalID, UInt32 frameNr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern uint get_frame_size(IntPtr ptr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr get_frame_data_ptr(IntPtr ptr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_track_frame(IntPtr ptr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr get_next_frame_for_track(IntPtr ptr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr add_client(IntPtr parentPtr, uint clientID);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr free_client(IntPtr clientPtr);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr add_track(IntPtr clientPtr, string trackID);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr add_tracks(IntPtr clientPtr, string[] trackID,
        byte[] isVideo, 
        uint count);
    [DllImport("WebRTCConnector", CallingConvention = CallingConvention.Cdecl)]
    public static extern void free_add_tracks_helper(IntPtr tracks_helper);

}
