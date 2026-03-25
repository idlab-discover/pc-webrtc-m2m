#pragma once
#include "connected_client.h"
#include <vector>

class WebRTCConnection
{
public:
	WebRTCConnection(unsigned int port_this, unsigned int port_remote);
	~WebRTCConnection();
	int connect();
	void disconnect();
	int wait_for_peer_connection();
	ConnectedClient* add_client(unsigned int client_id);
	unsigned int add_track(const std::string& track_id, bool is_video);
	std::vector<unsigned int> add_tracks(unsigned int client_id, char* const* track_ids, uint8_t* is_video, size_t count);
	int send_track_frame(unsigned int client_id, void* data, uint32_t size, uint32_t internal_id, uint32_t frame_nr);
private:
	bool is_listening_for_data;
	int connection_status = -1;
	unsigned int port_this;
	unsigned int port_remote;
	bool initialized = false;
	std::jthread worker;
	sockaddr_in si_send;
	SOCKET s_send;
	int slen_send = sizeof(si_send);
	sockaddr_in si_recv;
	SOCKET s_recv;
	int slen_recv = sizeof(si_recv);
	char* buf = NULL;
	char* buf_ori = NULL;
	bool keep_working = false;
	std::mutex m_receivers;
	std::mutex m_recv_data;
	std::mutex m_send_data;
	std::mutex m_recv_control;
	std::mutex m_peer_ready;
	std::condition_variable cv_peer_ready;
	std::condition_variable cv_listening_for_data;
	bool peer_ready = false;
	std::map<uint32_t, ConnectedClient*> clients;
	std::map<std::string, uint32_t> track_name_to_id;
	unsigned int track_id_counter = 0;
	std::vector<std::vector<char>> pending_track_packets;
	#define BUFLEN 1300

	#ifdef WIN32
	WSADATA wsa;
	#endif

	
	void listen_for_data();
	ConnectedClient* find_client(unsigned int client_id);
	
	
	char* serialize_tracks_name_to_id(size_t& out_size);
	int send_packet(char* data, uint32_t size, uint32_t _packet_type);
	int send_remote_client_track_packet(void* data, uint32_t size);

	void write_u32_le(char*& buffer, uint32_t value);
	
	
};

