#pragma once
#include "webrtc_connection.h"
#ifdef WIN32
#define DLLExport __declspec(dllexport)
#else
#define DLLExport
#endif

// All exported functions should be declared here
extern "C"
{
	DLLExport void set_logging(char* log_directory, int _log_level);
	DLLExport int initialize(char* ip_send, uint32_t port_send, char* ip_recv, uint32_t port_recv, uint32_t n_capturers, uint32_t n_tiles,
		uint32_t client_id, char* api_version);
	DLLExport void listen_for_data();
	DLLExport void clean_up();
	DLLExport int send_tile(void* data, uint32_t size, uint32_t capturer_id, uint32_t tile_id);
	DLLExport int get_tile_size(uint32_t client_id, uint32_t capturer_id, uint32_t tile_id);
	DLLExport int get_tile_frame_number(uint32_t client_id, uint32_t capturer_id, uint32_t tile_id);
	DLLExport void retrieve_tile(void* buff, uint32_t size, uint32_t client_id, uint32_t capturer_id, uint32_t tile_id);
	DLLExport int send_audio(void* data, uint32_t size);
	DLLExport int get_audio_size(uint32_t client_id);
	DLLExport void retrieve_audio(void* buff, uint32_t size, uint32_t client_id);
	DLLExport int send_control(void* data, uint32_t size);
	DLLExport int get_control_size();
	DLLExport void retrieve_control(void* buff, uint32_t size);
	DLLExport int send_control_packet(void* data, uint32_t size);
	DLLExport int send_intrisics_packet(void* data, uint32_t size);
	DLLExport void wait_for_peer();

	DLLExport WebRTCConnection* create_new_webrtc_connection(uint32_t port_this, uint32_t port_remote);
	DLLExport void free_webrtc_connection(WebRTCConnection* conn);
	DLLExport ConnectedClient* add_client(WebRTCConnection* connection, unsigned int client_id);
	DLLExport void free_client(ConnectedClient* client);
	DLLExport TrackInternal* add_track(ConnectedClient* client, const char* track_id);
	DLLExport unsigned int get_frame_size(TrackFrame* frame);
	DLLExport char* get_frame_data_ptr(TrackFrame* frame);
	DLLExport void free_track_frame(TrackFrame* frame);
	DLLExport int wait_for_peer_connection(WebRTCConnection* connection);
	DLLExport int send_track_frame(WebRTCConnection* connection, void* data, uint32_t size, uint32_t internal_id, uint32_t frame_nr);
}
