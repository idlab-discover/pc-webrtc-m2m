#include "pch.h"
#include "framework.h"
#include "webrtc_connection.h"
#include "log.h"
#include "packet_data.hpp"
// TODO
// AddRemoteClient function


WebRTCConnection::WebRTCConnection(unsigned int port_this, unsigned int port_remote) : port_this(port_this), port_remote(port_remote)
{
	
}

WebRTCConnection::~WebRTCConnection()
{
	disconnect();
}

int WebRTCConnection::connect() {
	buf = (char*)malloc(BUFLEN);
	buf_ori = buf;
	keep_working = true;
	std::unique_lock<std::mutex> guard(m_receivers);
	clients = std::map<uint32_t, ConnectedClient*>();
	clients.clear();
	guard.unlock();
#ifdef WIN32
	if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
		return StartUpError;
	}
#endif

	// Generic parameters
	ULONG buf_size = 524288000;
	// Create send socket
	if ((s_send = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)) == SOCKET_ERROR) {
#ifdef WIN32
		WSACleanup();
#endif
		return SocketCreationError;
	}

	// Binding ports
	sockaddr_in our_address;
	our_address.sin_family = AF_INET;
	our_address.sin_port = htons(port_this);
	our_address.sin_addr.s_addr = htonl(INADDR_LOOPBACK);
	if (::bind(s_send, (struct sockaddr*)&our_address, sizeof(our_address)) < 0) {
		return -1;
	}

	// Set socket options
	if (setsockopt(s_send, SOL_SOCKET, SO_RCVBUF, (char*)&buf_size, sizeof(ULONG)) < 0) {
		return -1;
	}
	si_send.sin_family = AF_INET;
	si_send.sin_port = htons(port_remote);
#ifdef WIN32
	inet_pton(AF_INET, "127.0.0.1", &si_send.sin_addr.S_un.S_addr);
#else
	inet_pton(AF_INET, "127.0.0.1", &si_send.sin_addr.s_addr);
#endif

    worker = std::thread(&WebRTCConnection::listen_for_data, this);
	initialized = true;
}

void WebRTCConnection::disconnect()
{
}

void WebRTCConnection::listen_for_data() {
	// Enable the listening thread to join
	while (keep_working) {

		// Make sure only one process is listening to the socket
		std::unique_lock<std::mutex> guard(m_recv_data);

		// Attempt to receive data from the Golang peer
		size_t size = 0;

		if ((size = recvfrom(s_recv, buf, BUFLEN, 0, NULL, NULL)) == SOCKET_ERROR) {
			std::this_thread::sleep_for(std::chrono::milliseconds(100));
			continue;
		}
		//custom_log("listen_for_data: recvfrom: got " + std::to_string(size) + " bytes", Debug);

		// Extract the packet type
		struct PacketType p_type(&buf, size);

		/*
			Distinguish between different packet types:
				0: peer is ready to receive / send data
				1: point cloud frame
				2: audio frame
				3: control message
		*/
		switch (p_type.type) {
		case (PacketType::ReadyPacket): {
			// Peer is now ready to receive data
			std::unique_lock<std::mutex> lk(m_peer_ready);
			peer_ready = true;
			char t[BUFLEN] = { 0 };
			//custom_log("listen_for_data: connected to peer", Default, Color::Orange);
			if (sendto(s_send, t, BUFLEN, 0, (struct sockaddr*)&si_send, slen_send) == SOCKET_ERROR) {
				//custom_log("initialize: sendto: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
				WSACleanup();
				return;
			}
			lk.unlock();
			cv_peer_ready.notify_all();
			break;
		};
		case (PacketType::FramePacket): {
			struct PacketFrameHeader p_header(&buf, size);
			ConnectedClient* c = find_client(p_header.client_id);
			if (c == nullptr) {
				break;
			}
			TrackInternal& track = c->get_track(p_header.track_id);
			TrackFrame* frame = track.get_incomplete_frame(p_header.frame_number, p_header.frame_length);
			if(frame == nullptr) {
				break;
			}
			bool did_insert = frame->insert(buf, p_header.frame_offset, p_header.packet_length, size);
			if(!did_insert) {
				break;
			}
			if (frame->is_complete()) {
				track.add_frame_to_priority_queue(frame);
			}
		};
		

		case (PacketType::TrackStatusPacket): {
			// Extract the packet header
			//if (trackChangeCallbackInstance != nullptr) {
			//	struct TrackStatusChangedHeader p_header(&buf, size);
			///	trackChangeCallbackInstance(p_header.client_id, p_header.last_frame_nr, p_header.capturer_id, p_header.tile_nr, p_header.is_added);
			//}

			//break;
		};
		default:
			//custom_log("listen_for_data: ERROR: unknown packet type " + to_string(p_type.type), Default, Color::Red);
			guard.unlock();
			exit(EXIT_FAILURE);
			break;
		};
		// Reset the buffer pointer
		buf = buf_ori;

		// Release the mutex
		guard.unlock();
	}
}

ConnectedClient* WebRTCConnection::find_client(unsigned int client_id) {
    std::unique_lock<std::mutex> guard(m_receivers);
    auto it = clients.find(client_id);
    if (it == clients.end()) {
        return nullptr;
    }
    return it->second;
}

ConnectedClient* WebRTCConnection::add_client(unsigned int client_id, const std::vector<std::string>& track_ids) {
    std::unique_lock<std::mutex> guard(m_receivers);
    auto it = clients.find(client_id);
    if (it != clients.end()) {
        return it->second;
    }
	std::vector<uint32_t> track_ids_uint;
	for (const auto& track_name : track_ids) {
		track_name_to_id[track_name] = track_id_counter;
		track_ids_uint.push_back(track_id_counter);
		track_id_counter++;
	}
    ConnectedClient* client = new ConnectedClient(client_id, track_ids_uint);
    clients[client_id] = client;
    return client;
}