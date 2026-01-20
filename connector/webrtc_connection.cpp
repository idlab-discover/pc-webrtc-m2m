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
	std::unique_lock<std::mutex> lk(m_peer_ready);
	connection_status = -2;
	cv_peer_ready.notify_all();
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
	if ((s_recv = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)) == SOCKET_ERROR) {
#ifdef WIN32
		WSACleanup();
#endif
		return SocketCreationError;
	}

	// Binding ports
	sockaddr_in our_address;
	our_address.sin_family = AF_INET;
	our_address.sin_port = htons(port_this);
	our_address.sin_addr.s_addr = htonl(INADDR_ANY);
	if (::bind(s_recv, (struct sockaddr*)&our_address, sizeof(our_address)) < 0) {
		return -1;
	}

	// Set socket options
	if (setsockopt(s_recv, SOL_SOCKET, SO_RCVBUF, (char*)&buf_size, sizeof(ULONG)) < 0) {
		return -1;
	}
	si_send.sin_family = AF_INET;
	si_send.sin_port = htons(port_remote);
#ifdef WIN32
	inet_pton(AF_INET, "127.0.0.1", &si_send.sin_addr.S_un.S_addr);
#else
	inet_pton(AF_INET, "127.0.0.1", &si_send.sin_addr.s_addr);
#endif

    worker = std::jthread(&WebRTCConnection::listen_for_data, this);
	initialized = true;
	return ConnectionSuccess;
}

void WebRTCConnection::disconnect()
{
	// TODO Send disconnect message to peer maybe
	//WSACleanup();
	keep_working = false;
	closesocket(s_recv);
	std::unique_lock<std::mutex> lk_recv(m_recv_data);
	std::unique_lock<std::mutex> guard(m_send_data);
	
	if (initialized) {
#ifdef WIN32
		WSACleanup();
#endif
	}
	initialized = false;
	std::unique_lock<std::mutex> lk_peer(m_peer_ready);
	cv_peer_ready.notify_all();
	cv_listening_for_data.wait(lk_recv, [this] {return !is_listening_for_data; });
	
}

int WebRTCConnection::wait_for_peer_connection() {
	std::unique_lock<std::mutex> lk(m_peer_ready);
	cv_peer_ready.wait(lk, [this] {return peer_ready || !initialized; });
	return connection_status;
}

void WebRTCConnection::listen_for_data() {
	// Enable the listening thread to join
	std::unique_lock<std::mutex> lk_lst(m_recv_data);
	is_listening_for_data = true;
	lk_lst.unlock();
	while (keep_working) {

		// Make sure only one process is listening to the socket
		std::unique_lock<std::mutex> guard(m_recv_data);

		// Attempt to receive data from the Golang peer
		size_t size = 0;

		if ((size = recvfrom(s_recv, buf, BUFLEN, 0, NULL, NULL)) == SOCKET_ERROR) {
			std::this_thread::sleep_for(std::chrono::milliseconds(100));
			continue;
		}
		if (size == 0) {
			continue;
		}
		//custom_log("listen_for_data: recvfrom: got " + std::to_string(size) + " bytes", Debug);

		// Extract the packet type
		struct PacketType p_type(&buf, size);

		/*
			Distinguish between different packet types:
				0: peer is ready to receive / send data
				1: track frame
				2: track status packet
				3: disconnection packet
		*/
		switch (p_type.type) {
		case (PacketType::ReadyPacket): {
			// Peer is now ready to receive data
			std::unique_lock<std::mutex> lk(m_peer_ready);
			peer_ready = true;
			char t[BUFLEN] = { 0 };
			//custom_log("listen_for_data: connected to peer", Default, Color::Orange);
			if (sendto(s_recv, t, BUFLEN, 0, (struct sockaddr*)&si_send, slen_send) == SOCKET_ERROR) {
				//custom_log("initialize: sendto: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
				WSACleanup();
				return;
			}
			lk.unlock();
			connection_status = 1;
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
			if (frame == nullptr) {
				break;
			}
			bool did_insert = frame->insert(buf, p_header.frame_offset, p_header.packet_length, size);
			if (!did_insert) {
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
		case (PacketType::DisconnectPacket): {
			// TODO Maybe move this to disconnect method and call disconnect here, also maybe add custom packet with reason for disconnection
			std::unique_lock<std::mutex> lk(m_peer_ready);
			connection_status = -2;
			cv_peer_ready.notify_all();
			break;
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
	std::unique_lock<std::mutex> lk_lst2(m_recv_data);
	is_listening_for_data = false;
	lk_lst2.unlock();
	cv_listening_for_data.notify_all();
}

ConnectedClient* WebRTCConnection::find_client(unsigned int client_id) {
    std::unique_lock<std::mutex> guard(m_receivers);
    auto it = clients.find(client_id);
    if (it == clients.end()) {
        return nullptr;
    }
    return it->second;
}

ConnectedClient* WebRTCConnection::add_client(unsigned int client_id) {
    std::unique_lock<std::mutex> guard(m_receivers);
    auto it = clients.find(client_id);
    if (it != clients.end()) {
        return it->second;
    }
	ConnectedClient* client = new ConnectedClient(this, client_id);
	clients[client_id] = client;
	/*std::vector<uint32_t> track_ids_uint;
	for(unsigned int i = 0; i < count; i++) {
		std::string track_name(track_ids[i]);
		track_name_to_id[track_name] = track_id_counter;
		track_ids_uint.push_back(track_id_counter);
		track_id_counter++;
	}
    
	// TODO Send track mappings to golang peer
	size_t serialized_size = 0;
	char* serialized = serialize_tracks_name_to_id(serialized_size);
	send_remote_client_track_packet(serialized, (uint32_t)serialized_size);*/
    return client;
}

unsigned int WebRTCConnection::add_track(const std::string& track_id, bool is_video)
{
	std::unique_lock<std::mutex> guard(m_receivers);
	unsigned int internal_id = track_id_counter;
	track_name_to_id[track_id] = internal_id;
	track_id_counter++;
	return internal_id;
}

std::vector<unsigned int> WebRTCConnection::add_tracks(unsigned int client_id, char* const* track_ids, uint8_t* is_video, size_t count) {
	std::unique_lock<std::mutex> guard_recv(m_receivers);
	std::vector<uint32_t> track_ids_uint;
	for (unsigned int i = 0; i < count; i++) {
		std::string track_name(track_ids[i]);
		track_name_to_id[track_name] = track_id_counter;
		track_ids_uint.push_back(track_id_counter);
		track_id_counter++;
	}
	guard_recv.unlock();
	std::unique_lock<std::mutex> guard_send(m_receivers);

	// TODO Send track mappings to golang 
	size_t total_len = sizeof(client_id) + sizeof(count); // To save client ID and count
	std::vector<uint32_t> id_lengths;
	id_lengths.reserve(count);
	for (unsigned int i = 0; i < count; i++) {
		uint32_t len = static_cast<uint32_t>(strlen(track_ids[i]));
		id_lengths.push_back(len);
		total_len += sizeof(len); // To save the length of each track ID
		total_len += len; // Data of track ID
		total_len += sizeof(uint32_t); // To save internal ID
		total_len += sizeof(bool); // To save if video or audio

	}
	std::vector<char> data_to_send(total_len);
	char* data_ptr = data_to_send.data();
	write_u32_le(data_ptr, 1);
	write_u32_le(data_ptr, static_cast<uint32_t>(count));
	for (unsigned int i = 0; i < count; i++) {
		write_u32_le(data_ptr, id_lengths[i]);
		memcpy(data_ptr, track_ids[i], id_lengths[i]); // Track ID data
		data_ptr += id_lengths[i];
		uint32_t internal_id = track_name_to_id[std::string(track_ids[i])];
		write_u32_le(data_ptr, internal_id);
		bool is_vid = is_video[i] != 0;
		memcpy(data_ptr, &is_vid, sizeof(bool));
		data_ptr += sizeof(bool);
	}

	send_packet(data_to_send.data(), total_len, PacketType::RemoteClientTracksPacket); // TODO Fill in data and size
	return track_ids_uint;
}

int WebRTCConnection::send_track_frame(unsigned int client_id, void* data, uint32_t size, uint32_t internal_id, uint32_t frame_nr)
{
	std::unique_lock<std::mutex> guard(m_send_data);
	if (!initialized) {
		return -1;
	}

	// Required parameters
	uint32_t buflen_nheader = BUFLEN - sizeof(PacketType) - sizeof(PacketHeader);
	buflen_nheader = 1148; // TODO check this, pretty sure this can be bigger
	uint32_t current_offset = 0;
	uint32_t remaining = size;
	int full_size_sent = 0;
	char* temp_d = reinterpret_cast<char*>(data);

	// Make sure only one process is sending out packets
	

	// Send out packets as long as needed
	while (remaining > 0 && keep_working) {

		// Determine the amount of bytes to send out
		uint32_t next_size = 0;
		if (remaining >= buflen_nheader) {
			next_size = buflen_nheader;
		}
		else {
			next_size = remaining;
		}
		PacketFrameHeader frame_header {
			client_id, internal_id, frame_nr, size, current_offset, next_size
		};
	
		// Insert all data into a buffer
		char buf_msg[BUFLEN];
		memcpy(buf_msg, &frame_header, sizeof(frame_header));
		memcpy(buf_msg + sizeof(frame_header), reinterpret_cast<char*>(data) + current_offset, next_size);

		// Send out the packet
		int size_sent = send_packet(buf_msg, next_size + sizeof(PacketFrameHeader), PacketType::FramePacket);
		if (size_sent < 0) {
			guard.unlock();
			return -1;
		}

		// Update parameters
		full_size_sent += size_sent;
		current_offset += next_size;
		remaining -= next_size;
	}

	// Release the mutex
	guard.unlock();

	// Return the amount of bytes sent
	return full_size_sent;
}



char* WebRTCConnection::serialize_tracks_name_to_id(size_t& out_size) {
    // Calculate total size needed
    size_t total_size = sizeof(uint32_t); // number of entries
    for (const auto& entry : track_name_to_id) {
        total_size += sizeof(uint32_t); // length of trackID
        total_size += entry.first.size(); // trackID string bytes
        total_size += sizeof(uint32_t); // internal ID
    }

    char* buffer = new char[total_size];
    char* ptr = buffer;

    // Write number of entries
    uint32_t num_entries = static_cast<uint32_t>(track_name_to_id.size());
    memcpy(ptr, &num_entries, sizeof(uint32_t));
    ptr += sizeof(uint32_t);

    // Write each entry
    for (const auto& entry : track_name_to_id) {
        uint32_t track_id_len = static_cast<uint32_t>(entry.first.size());
        memcpy(ptr, &track_id_len, sizeof(uint32_t));
        ptr += sizeof(uint32_t);

        memcpy(ptr, entry.first.data(), track_id_len);
        ptr += track_id_len;

        uint32_t internal_id = entry.second;
        memcpy(ptr, &internal_id, sizeof(uint32_t));
        ptr += sizeof(uint32_t);
    }

    out_size = total_size;

    return buffer;
}

/*
	This function allows to send out a packet to the Golang peer. It returns the amount of bytes sent.
*/
int WebRTCConnection::send_packet(char* data, uint32_t size, uint32_t _packet_type) {
	// Required parameters
	uint32_t packet_type = _packet_type;
	int size_sent = 0;

	// Insert all data into a buffer
	char buf_msg[BUFLEN] = { 0 };
	memcpy(buf_msg, &packet_type, sizeof(packet_type));
	memcpy(&buf_msg[sizeof(packet_type)], data, size);

	// Send the message to the Golang peer
	if ((size_sent = sendto(s_recv, buf_msg, BUFLEN, 0, (struct sockaddr*)&si_send, slen_send)) == SOCKET_ERROR) {
		return -1;
	}

	// Return the amount of bytes sent
	return size_sent;
}

/*
	This function allows to send out an audio frame to the Golang peer. It returns the amount of bytes sent.
*/
int WebRTCConnection::send_remote_client_track_packet(void* data, uint32_t size) {
	if (!initialized) {
		return -1;
	}

	// Required parameters
	int full_size_sent = 0;
	char* temp_d = reinterpret_cast<char*>(data);

	// Make sure only one process is sending out packets
	std::unique_lock<std::mutex> guard(m_send_data);

	// Send out packets as long as needed
	// Determine the amount of bytes to send out

	// Insert all data into a buffer
	char buf_msg[BUFLEN];
	memcpy(buf_msg, reinterpret_cast<char*>(data), size);

	// Send out the packet
	int size_sent = send_packet(buf_msg, size, PacketType::RemoteClientTracksPacket);
	if (size_sent < 0) {
		guard.unlock();
		
		return -1;
	}

	// Update parameters
	full_size_sent += size_sent;
	guard.unlock();

	// Return the amount of bytes sent
	return full_size_sent;
}

void WebRTCConnection::write_u32_le(char*& buffer, uint32_t value)
{
	buffer[0] = static_cast<char>(value & 0xFF);
	buffer[1] = static_cast<char>((value >> 8) & 0xFF);
	buffer[2] = static_cast<char>((value >> 16) & 0xFF);
	buffer[3] = static_cast<char>((value >> 24) & 0xFF);
	buffer += sizeof(uint32_t);
}
