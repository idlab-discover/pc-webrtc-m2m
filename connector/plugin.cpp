#include "pch.h"
#include "buffer.hpp"
#include "audio_buffer.hpp"
#include "client_receiver.hpp"
#include "data_parser.hpp"
#include "framework.h"
#include "log.h"
#include "packet_data.hpp"
#include "plugin.h"
#include "received_control.hpp"

#include <chrono>
#include <fstream>
#include <iostream>
#include <map>
#include <string>
#include <thread>

// Maximum length of buffer
#define BUFLEN 1300

using namespace std;

bool one_socket = true;
#ifdef WIN32
static WSADATA wsa;
//#define SIO_UDP_CONNRESET _WSAIOW(IOC_VENDOR, 12)
#endif
static struct sockaddr_in si_send;
static SOCKET s_send;
int slen_send = sizeof(si_send);
static struct sockaddr_in si_recv;
static SOCKET s_recv;
int slen_recv = sizeof(si_recv);

uint32_t client_id;
uint32_t n_tiles;

static thread worker;
static bool keep_working = true;
static bool initialized = false;
static bool peer_ready = false;

mutex m_receivers;
mutex m_recv_data;
mutex m_send_data;
mutex m_recv_control;
mutex m_peer_ready;

condition_variable cv_peer_ready;

static char* buf = NULL;
static char* buf_ori = NULL;


map<uint32_t, ClientReceiver*> client_receivers;

vector<uint32_t> frame_numbers;
uint32_t audio_frame_number;
queue<ReceivedControl> recv_controls;

static string log_file = "";
static int log_level = 0;
mutex m_logging;

string api_version = "1.0";

extern "C"
{
	typedef void(*TrackChangeCallBack)(uint32_t client_id, uint32_t frame_nr, uint32_t tile_nr, bool is_added);
	static TrackChangeCallBack trackChangeCallbackInstance = nullptr;
	DLLExport void register_track_change_callback(TrackChangeCallBack cb);
}
void register_track_change_callback(TrackChangeCallBack cb) {
	trackChangeCallbackInstance = cb;
}

enum CONNECTION_SETUP_CODE : int {
	ConnectionSuccess = 0,
	StartUpError = 1,
	SocketCreationError = 2,
	SendToError = 3,
	AlreadyInitialized = 4,
	WrongAPIVersion = 5
};

enum LOG_LEVEL : int {
	Default = 0,
	Verbose = 1,
	Debug = 2
};

/*
	This function is used to get the current date/time in a predefined format, used by the custom_log function.
*/
inline string get_current_date_time(bool date_only) {
	time_t now = time(0);
	char buf[80];
	struct tm tstruct;
#if defined(_WIN64) || defined(_WIN32)
	localtime_s(&tstruct, &now);
#else
	localtime_r(&now, &tstruct);
#endif
	if (date_only) {
		strftime(buf, sizeof(buf), "%Y-%m-%d", &tstruct);
	}
	else {
		strftime(buf, sizeof(buf), "%Y-%m-%d %X", &tstruct);
	}
	return string(buf);
};

/*
	This function is used to pass log messages to the user. Verbose logging can be enabled, and different colors can be
	used to inidicate a specific function (e.g., sending or receiving data).
*/
void custom_log(string message, int _log_level = 0, Color color = Color::Black) {
	unique_lock<mutex> guard(m_logging);
	if (_log_level <= log_level) {
		Log::log(message, color);
	}
	//if (log_file != "") {
	//	Log::log("test", color);
	//	ofstream ofs(log_file.c_str(), ios_base::out | ios_base::app);
	//	ofs << get_current_date_time(false) << '\t' << message << '\n';
	//	ofs.close();
	//}
	guard.unlock();
}

/*
	This function allows to specify a directory in which logs are created, and allows to specify if a verbose mode
	should be used. It should be called once per session from within Unity.
*/
void set_logging(char* log_directory, int _log_level) {
	log_file = string(log_directory) + "\\" + get_current_date_time(true) + ".txt";
	Log::log("set_logging: Log directory set to " + string(log_directory), Color::Orange);
	log_level = _log_level;
	Log::log("set_logging: Log level set to " + to_string(log_level), Color::Orange);
}

/*
	This function enables the retrieval and (if needed) the creation of a client receiver. One receivere is required
	per WebRTC connection, so that frames and tiles corresponding to this peer can be stored appropriately.
*/
ClientReceiver* find_or_add_receiver(uint32_t client_id, bool is_audio = false) {
	unique_lock<mutex> guard(m_receivers);
	ClientReceiver* c = nullptr;
	
		auto it = client_receivers.find(client_id);
		if (it == client_receivers.end()) {
			
			custom_log("find_or_add_receiver: Client ID " + to_string(is_audio) + to_string(client_id) + " not yet registered, inserting now",
				Default, Color::Orange);
			//Sleep(5000);
			c = new ClientReceiver(client_id, n_tiles);
			//if (!is_audio) {
			//Sleep(5000);
			// We need to do it like this because else bad stuff happens when we use both audio + video
			auto itt = client_receivers.insert({ client_id, c });
			c = itt.first->second;
			custom_log("find_or_add_receiver: Client ID " + to_string(client_id) +
				" registered. Make sure you are receiving the correct client ID!", Verbose, Color::Orange);
			//	Sleep(5000);
		}
		else {
			c = it->second;
		}
	
	
	
	
	guard.unlock();
	return c;
}

/*
	This function is responsible for initializing the DLL. It should be called once per session from within Unity,
	specifiying the required IP addresses and ports, the number of tiles that will be transmitted, and the client ID.
*/
int initialize(char* ip_send, uint32_t port_send, char* ip_recv, uint32_t port_recv, uint32_t _n_tiles,
	uint32_t _client_id, char* api_version) {

	if (string(api_version) != api_version) {
		custom_log("intialize: ERROR: An incorrect API version is used!");
		return WrongAPIVersion;
	}

	custom_log("initialize: Attempting to connect to sender " + string(ip_send) + ":" + to_string(port_send) +
		" and receiver " + string(ip_recv) + ":" + to_string(port_recv) + ", using n_tiles " + to_string(_n_tiles) +
		" and client_id " + to_string(_client_id), Verbose, Color::Orange);

	// Check if the DLL has already been initialized
	if (initialized) {
		custom_log("initialize: DLL already initialized, no changes are possible", Verbose, Color::Orange);
		return AlreadyInitialized;
	}

	// Initialize parameters
	client_id = _client_id;
	n_tiles = _n_tiles;
	frame_numbers = vector<uint32_t>(n_tiles, 0);
	buf = (char*)malloc(BUFLEN);
	buf_ori = buf;
	keep_working = true;
	unique_lock<mutex> guard(m_receivers);
	client_receivers = map<uint32_t, ClientReceiver*>();
	client_receivers.clear();
	guard.unlock();
#ifdef WIN32
	custom_log("initialize: Setting up socket to " + string(ip_send), Verbose, Color::Orange);
	if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
		return StartUpError;
	}
#endif

	// Generic parameters
	ULONG buf_size = 524288000;
	char t[BUFLEN] = { 0 };
	t[0] = (char)n_tiles;

	// Create send socket
	if ((s_send = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)) == SOCKET_ERROR) {
		custom_log("initialize: socket: ERROR: Failed to create socket", Default, Color::Red);
		WSACleanup();
		return SocketCreationError;
	}

	// Binding ports
	sockaddr_in our_address;
	our_address.sin_family = AF_INET;
	our_address.sin_port = htons(port_send + 1);
	our_address.sin_addr.s_addr = htonl(INADDR_ANY);
	if (::bind(s_send, (struct sockaddr*)&our_address, sizeof(our_address)) < 0) {
		custom_log("initialize: bind: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
	}

	// Set socket options
	if (setsockopt(s_send, SOL_SOCKET, SO_RCVBUF, (char*)&buf_size, sizeof(ULONG)) < 0) {
		custom_log("initialize: setsockopt: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
	}
	si_send.sin_family = AF_INET;
	si_send.sin_port = htons(port_send);
#ifdef WIN32
	inet_pton(AF_INET, ip_send, &si_send.sin_addr.S_un.S_addr);
#else
	inet_pton(AF_INET, ip_send, &si_send.sin_addr.s_addr);
#endif

	// TODO: remove this?
	sockaddr_in our_addr;
	socklen_t our_addr_len = sizeof(our_addr);
	getsockname(s_send, (sockaddr*)&our_addr, &our_addr_len);
	custom_log("initialize: getsockname: Our port is " + to_string(ntohs(our_addr.sin_port)) + ", their port is " +
		to_string(ntohs(si_send.sin_port)), Verbose, Color::Orange);
	//BOOL bNewBehavior = FALSE;
	//DWORD dwBytesReturned = 0;
	//WSAIoctl(s_send, SIO_UDP_CONNRESET, &bNewBehavior, sizeof bNewBehavior, NULL, 0, &dwBytesReturned, NULL, NULL);
	// Send a message to the Golang peer, containing the number of tiles (<10 for now)
	/*if (sendto(s_send, t, BUFLEN, 0, (struct sockaddr*)&si_send, slen_send) == SOCKET_ERROR) {
		custom_log("initialize: sendto: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
		WSACleanup();
		return SendToError;
	}*/

	// In case a separate port is used to receive incoming data
	if (string(ip_send) != string(ip_recv) || port_send != port_recv) {

		// Make sure the listening thread is aware that a separate port is used
		one_socket = false;

		// Sleep for a while, so that conflicts in the WebRTC signaling can be avoided
		this_thread::sleep_for(chrono::milliseconds(2000));

		// Create receive socket
		if ((s_recv = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)) == SOCKET_ERROR) {
			custom_log("initialize: socket: ERROR: Failed to create socket", Default, Color::Red);
			WSACleanup();
			return SocketCreationError;
		}

		// Set socket options
		if (setsockopt(s_recv, SOL_SOCKET, SO_RCVBUF, (char*)&buf_size, sizeof(ULONG)) < 0) {
			custom_log("initialize: setsockopt: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
		}
		si_recv.sin_family = AF_INET;
		si_recv.sin_port = htons(port_recv);
#ifdef WIN32
		inet_pton(AF_INET, ip_recv, &si_recv.sin_addr.S_un.S_addr);
#else
		inet_pton(AF_INET, ip_recv, &si_recv.sin_addr.s_addr);
#endif

		// TODO: remove this?
		sockaddr_in our_addr;
		socklen_t our_addr_len = sizeof(our_addr);
		getsockname(s_send, (sockaddr*)&our_addr, &our_addr_len);
		custom_log("initialize: getsockname: Our port is " + to_string(our_addr.sin_port) + ", their port is " +
			to_string(si_recv.sin_port), Verbose, Color::Orange);
		//BOOL bNewBehavior = FALSE;
		//DWORD dwBytesReturned = 0;
		//WSAIoctl(s_recv, SIO_UDP_CONNRESET, &bNewBehavior, sizeof bNewBehavior, NULL, 0, &dwBytesReturned, NULL, NULL);
		// Send a message to the Golang peer, containing the number of tiles (<10 for now)
		// TODO rework this 
		if (sendto(s_recv, t, BUFLEN, 0, (struct sockaddr*)&si_recv, slen_recv) == SOCKET_ERROR) {
			custom_log("initialize: sendto: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
			WSACleanup();
			return SendToError;
		}
	}

	// Start a separate thread to listen to incoming data
	custom_log("initialize: Starting new listening thread", Verbose, Color::Yellow);
	worker = thread(listen_for_data);

	// Make sure the clean_up function is aware
	initialized = true;

	return ConnectionSuccess;
}

/*
	This function is responsible for capturing incoming data. It is called from within a thread, which is started by the
	initialize function. No action is required from within Unity.
*/
void listen_for_data() {
	custom_log("listen_for_data: Starting to listen for incoming data", Verbose, Color::Yellow);

	// Enable the listening thread to join
	while (keep_working) {

		// Make sure only one process is listening to the socket
		unique_lock<mutex> guard(m_recv_data);

		// Attempt to receive data from the Golang peer
		size_t size = 0;
		if (one_socket) {
			// Receive from the only available socket
			if ((size = recvfrom(s_send, buf, BUFLEN, 0, NULL, NULL)) == SOCKET_ERROR) {
				custom_log("listen_for_data: recvfrom: ERROR: " + std::to_string(WSAGetLastError()), Default,
					Color::Red);
				//std::this_thread::sleep_for(std::chrono::milliseconds(100));
				continue;
			}
		}
		else {
			// Receive from the additional socket
			if ((size = recvfrom(s_recv, buf, BUFLEN, 0, NULL, NULL)) == SOCKET_ERROR) {
				custom_log("listen_for_data: recvfrom: ERROR: " + std::to_string(WSAGetLastError()), Default,
					Color::Red);
				std::this_thread::sleep_for(std::chrono::milliseconds(100));
				continue;
			}
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
				unique_lock<mutex> lk(m_peer_ready);
				peer_ready = true;
				char t[BUFLEN] = { 0 };
				t[0] = (char)n_tiles;
				custom_log("listen_for_data: connected to peer", Default, Color::Orange);
				if (sendto(s_send, t, BUFLEN, 0, (struct sockaddr*)&si_send, slen_send) == SOCKET_ERROR) {
					custom_log("initialize: sendto: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
					WSACleanup();
					return;
				}
				lk.unlock();
				cv_peer_ready.notify_all();
				break;
			};
			case (PacketType::TilePacket): {
				// Extract the packet header
				struct PacketHeader p_header(&buf, size);

				// Get the receiver belonging to the connected peer
				ClientReceiver* c = find_or_add_receiver(p_header.client_id);

				// Retrieve tile iterator, creating a new instance if needed
				auto tile = c->recv_tiles.find(make_pair(p_header.frame_number, p_header.tile_id));
				if (tile == c->recv_tiles.end()) {
					auto e = c->recv_tiles.emplace(make_pair(p_header.frame_number, p_header.tile_id),
						ReceivedTile(p_header.file_length, p_header.frame_number, p_header.tile_id));
					tile = e.first;
					custom_log("listen_for_data: New tile: " + p_header.string_representation(), Debug, Color::Yellow);
				}

				// Insert the new data
				bool inserted = tile->second.insert(buf, p_header.file_offset, p_header.packet_length, size);
				if (!inserted) {
					//custom_log("listen_for_data: Failed to insert: " + p_header.string_representation(), Default,
					//	Color::Red);
				}

				// Check if all data corresponding to the frame has arrived
				if (tile->second.is_complete()) {
					if (tile->second.get_frame_number() % 10 == 0) {
						custom_log("listen_for_data: Completed: " + to_string(p_header.frame_number) + " " + to_string(p_header.tile_id), Default, Color::Yellow);
					}
					c->tile_buffer.insert_tile(tile->second, p_header.tile_id);
					c->recv_tiles.erase(make_pair(p_header.frame_number, p_header.tile_id));
					//custom_log("listen_for_data: New buffer size: " +
					//	to_string(c->tile_buffer.get_buffer_size(p_header.tile_id)), Debug, Color::Yellow);
				}
				break;
			};
			case (PacketType::AudioPacket): {
				// Extract the packet header
				struct AudioPacketHeader p_header(&buf, size);


				//custom_log("listen_for_data: New audio: " + std::to_string(AudioPacketHeader::size()) + " " + std::to_string(size), Default);

				// Get the receiver belonging to the connected peer
				ClientReceiver* c = find_or_add_receiver(p_header.client_id);

				// Retrieve tile iterator, creating a new instance if needed
				auto audio_frame = c->recv_audio.find(p_header.frame_number);
				if (audio_frame == c->recv_audio.end()) {
					auto e = c->recv_audio.emplace(p_header.frame_number,
						ReceivedAudio(p_header.file_length, p_header.frame_number));
					audio_frame = e.first;
					if (p_header.frame_number % 100 == 0)
						custom_log("listen_for_data: New audio: " + std::to_string(p_header.file_offset) + " " + std::to_string(p_header.packet_length) + std::to_string(p_header.file_length), Debug);
				}


				// Insert the new data
				bool inserted = audio_frame->second.insert(buf, p_header.file_offset, p_header.packet_length, size);
				if (!inserted) {
					//custom_log("listen_for_data: Failed to insert: " + p_header.string_representation(), Default,
					//	Color::Red);
				}
				// Check if all data corresponding to the frame has arrived
				if (audio_frame->second.is_complete()) {
					//custom_log("listen_for_data: Completed: " + p_header.string_representation(), Verbose, Color::Yellow);
					c->audio_buffer.insert_audio_frame(audio_frame->second);
					c->recv_audio.erase(p_header.frame_number);
					//	custom_log("listen_for_data: New buffer size: " +
					//		to_string(c->audio_buffer.get_buffer_size()), Verbose, Color::Yellow);
					//	custom_log("listen_for_data: done", Default);
				}
				break;
			};
			case (PacketType::ControlPacket): {
				// Push the message
				recv_controls.push(ReceivedControl(buf, (uint32_t)size));
				break;
			};
			case (PacketType::TrackStatusPacket): {
				// Extract the packet header
				if (trackChangeCallbackInstance != nullptr) {
					struct TrackStatusChangedHeader p_header(&buf, size);
					trackChangeCallbackInstance(p_header.client_id, p_header.last_frame_nr, p_header.tile_nr, p_header.is_added);
				}
				
				break;
			};
			default:
				custom_log("listen_for_data: ERROR: unknown packet type " + to_string(p_type.type), Default, Color::Red);
				guard.unlock();
				exit(EXIT_FAILURE);
				break;
		};
		// Reset the buffer pointer
		buf = buf_ori;

		// Release the mutex
		guard.unlock();
	}

	// Stopped listening
	custom_log("listen_for_data: Stopped listening for incoming data", Verbose, Color::Yellow);
}

/*
	This function is used to clean up threading and reset the required variables. It is called once per session from
	within Unity.
*/
void clean_up() {
	custom_log("clean_up: Attempting to clean up", Verbose, Color::Orange);

	// If we close before peer is started we still want to wake up all the sleeping threads
	cv_peer_ready.notify_all();
	// Check if the DLL has already been initialized
	if (initialized) {

		// Halt sending/receiving operations
		keep_working = false;

		// Close sockets, using the mutex for sending data
		unique_lock<mutex> guard(m_send_data);
		closesocket(s_recv);
		closesocket(s_send);
		frame_numbers.clear();
		guard.unlock();

		// Join the listening thread
		if (worker.joinable())
			worker.join();

		// Free allocated memory
		if (buf != NULL) {
			free(buf);
			buf = NULL;
		}

		// Erase all client receivers
		for (auto it = client_receivers.cbegin(); it != client_receivers.cend(); ) {
			client_receivers.erase(it++);
		}

		// Reset the initialized flag
		initialized = false;

		custom_log("clean_up: Cleaned up", Verbose, Color::Orange);
	}
	else {

		// No action is required
		custom_log("clean_up: Already cleaned up", Verbose, Color::Orange);
	}
}

/*
	This function allows to send out a packet to the Golang peer. It returns the amount of bytes sent.
*/
int send_packet(char* data, uint32_t size, uint32_t _packet_type) {
	// Required parameters
	uint32_t packet_type = _packet_type;
	int size_sent = 0;

	// Insert all data into a buffer
	char buf_msg[BUFLEN] = { 0 };
	memcpy(buf_msg, &packet_type, sizeof(packet_type));
	memcpy(&buf_msg[sizeof(packet_type)], data, size);

	// Send the message to the Golang peer
	if ((size_sent = sendto(s_send, buf_msg, BUFLEN, 0, (struct sockaddr*)&si_send, slen_send)) == SOCKET_ERROR) {
		custom_log("send_packet: sendto: ERROR: " + std::to_string(WSAGetLastError()), Default, Color::Red);
		return -1;
	}

	// Return the amount of bytes sent
	return size_sent;
}

/*
	This function allows to send out a frame of a tile to the Golang peer. It returns the amount of bytes sent.
*/
int send_tile(void* data, uint32_t size, uint32_t tile_id) {
	custom_log("send_tile: Tile " + to_string(tile_id) + " with size " + to_string(size), Debug, Color::Green);

	if (!initialized) {
		custom_log("send_tile: ERROR: The DLL has not yet been initialized!", Default, Color::Red);
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
	unique_lock<mutex> guard(m_send_data);

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

		// Create a new packet header
		struct PacketHeader p_header {
			client_id, frame_numbers[tile_id], size, current_offset, next_size, tile_id
		};

		// Insert all data into a buffer
		char buf_msg[BUFLEN];
		memcpy(buf_msg, &p_header, sizeof(p_header));
		memcpy(buf_msg + sizeof(p_header), reinterpret_cast<char*>(data) + current_offset, next_size);

		// Send out the packet
		int size_sent = send_packet(buf_msg, next_size + sizeof(PacketHeader), PacketType::TilePacket);
		if (size_sent < 0) {
			guard.unlock();
			custom_log("send_tile: ERROR: the return value of send_packet should not be negative!", Default,
				Color::Red);
			return -1;
		}

		// Update parameters
		full_size_sent += size_sent;
		current_offset += next_size;
		remaining -= next_size;
	}

	custom_log("send_tile: Sent out frame " + to_string(frame_numbers[tile_id]) + " of tile " +
		to_string(tile_id) + ", using " + to_string(full_size_sent) + " bytes", Debug, Color::Green);

	// Increase frame number by one
	frame_numbers[tile_id] += 1;

	// Release the mutex
	guard.unlock();

	// Return the amount of bytes sent
	return full_size_sent;
}

/*
	This function returns the size of the next frame corresponding to a given tile. The function should be called from
	within the Unity reader every time a new frame is desired. The resulting return value should be used to allocate the
	required memory and call the retrieve_tile function.
*/
int get_tile_size(uint32_t client_id, uint32_t tile_id) {
	custom_log("get_tile_size: " + to_string(client_id) + ", " + to_string(tile_id), Debug, Color::Yellow);

	// Get the receiver belonging to the connected peer
	ClientReceiver* c = find_or_add_receiver(client_id);

	// Wait until a new frame is available
	while (c->tile_buffer.get_buffer_size(tile_id) == 0) {
		this_thread::sleep_for(chrono::milliseconds(1));
		
		if (!keep_working) {
			return 0;
		}
	}

	// Retrieve the next frame and forward it to the data parser
	ReceivedTile t = c->tile_buffer.next(tile_id);
	c->data_parser.set_current_tile(t, tile_id);

	// Retrieve the tile size
	int tile_size = c->data_parser.get_current_tile_size(tile_id);
	custom_log("get_tile_size: return " + to_string(tile_size), Debug, Color::Yellow);

	// Return the tile size
	return tile_size;
}


/*
	This function allows to retrieve the next frame corresponding to a tile in memory. The function should be called
	from within the Unity reader once get_tile_size has returned the tile size and the required memory has been
	allocated.
*/
void retrieve_tile(void* d, uint32_t size, uint32_t client_id, uint32_t tile_id) {
	custom_log("retrieve_tile: " + to_string(client_id) + ", " + to_string(tile_id), Debug, Color::Yellow);

	// Get the receiver belonging to the connected peer
	ClientReceiver* c = find_or_add_receiver(client_id);

	// Fill the allocated memory with the requested data, if possible
	int local_size = c->data_parser.fill_data_array(d, size, tile_id);
	if (local_size == 0) {
		custom_log("retrieve_tile: ERROR: the tile could not be retrieved", Default, Color::Red);
	}
	else if (local_size != size) {
		custom_log("retrieve_tile: ERROR: retrieve_tile parameter size " + to_string(size) +
			" does not match the registered data length" + to_string(local_size), Default, Color::Red);
	}
	else {
		custom_log("retrieve_tile: Tile " + to_string(tile_id) + " from client " + to_string(client_id) +
			" successfully retrieved", Debug, Color::Yellow);
	}
}

/*
	This function allows to retrieve the next frame corresponding to a tile in memory. The function should be called
	from within the Unity reader once get_tile_size has returned the tile size and the required memory has been
	allocated.
*/
int get_tile_frame_number(uint32_t client_id, uint32_t tile_id) {
	custom_log("get_tile_frame_number: " + to_string(client_id) + ", " + to_string(tile_id), Debug, Color::Yellow);

	// Get the receiver belonging to the connected peer
	ClientReceiver* c = find_or_add_receiver(client_id);

	return c->data_parser.get_current_tile_nr(tile_id);
}


/*
	This function allows to send out an audio frame to the Golang peer. It returns the amount of bytes sent.
*/
int send_audio(void* data, uint32_t size) {
	custom_log("send_audio: Size " + to_string(size), Debug, Color::Green);

	if (!initialized) {
		custom_log("send_tile: ERROR: The DLL has not yet been initialized!", Default, Color::Red);
		return -1;
	}

	// Required parameters
	uint32_t buflen_nheader = BUFLEN - sizeof(PacketType) - sizeof(PacketHeader);
	buflen_nheader = 1152; // TODO check this, pretty sure this can be bigger
	uint32_t current_offset = 0;
	uint32_t remaining = size;
	int full_size_sent = 0;
	char* temp_d = reinterpret_cast<char*>(data);

	// Make sure only one process is sending out packets
	unique_lock<mutex> guard(m_send_data);

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

		// Create a new packet header
		struct AudioPacketHeader p_header {
			client_id, audio_frame_number, size, current_offset, next_size
		};

		// Insert all data into a buffer
		char buf_msg[BUFLEN];
		memcpy(buf_msg, &p_header, sizeof(p_header));
		memcpy(buf_msg + sizeof(p_header), reinterpret_cast<char*>(data) + current_offset, next_size);

		// Send out the packet
		int size_sent = send_packet(buf_msg, next_size + sizeof(AudioPacketHeader), PacketType::AudioPacket);
		if (size_sent < 0) {
			guard.unlock();
			custom_log("send_tile: ERROR: the return value of send_packet should not be negative!", Default,
				Color::Red);
			return -1;
		}

		// Update parameters
		full_size_sent += size_sent;
		current_offset += next_size;
		remaining -= next_size;
	}

	custom_log("send_audio: Sent out audio frame " + to_string(audio_frame_number) + ", using " + to_string(full_size_sent) + " bytes", Debug, Color::Green);

	// Increase frame number by one
	audio_frame_number += 1;

	// Release the mutex
	guard.unlock();

	// Return the amount of bytes sent
	return full_size_sent;
}

/*
	This function returns the size of the next audio frame corresponding. The function should be called from
	within the Unity reader every time a new audio frame is desired. The resulting return value should be used to allocate the
	required memory and call the retrieve_audio function.
*/
int get_audio_size(uint32_t client_id) {
	custom_log("get_audio_size: " + to_string(client_id), Default, Color::Yellow);
	
	// Get the receiver belonging to the connected peer
	ClientReceiver* c = find_or_add_receiver(client_id);
	
	// Wait until a new frame is available
	//timeBeginPeriod(1);
	while (c->audio_buffer.get_buffer_size() == 0) {
		this_thread::sleep_for(chrono::milliseconds(1)); // TODO: remove all of this pull logic and replace it with callbacks
		c = find_or_add_receiver(client_id);
		if (!keep_working) {
			return 0;
		}
	}
	//timeEndPeriod(1);
	// Retrieve the next frame and forward it to the data parser
	ReceivedAudio t = c->audio_buffer.next();
	c->data_parser.set_current_audio(t);

	// Retrieve the tile size
	int audio_size = c->data_parser.get_current_audio_size();
	custom_log("get_audio_size: return " + to_string(audio_size), Debug, Color::Yellow);

	// Return the tile size
	return audio_size;
}


/*
	This function allows to retrieve the next audio frame from memory. The function should be called
	from within the Unity reader once get_audio_size has returned the audio size and the required memory has been
	allocated.
*/
void retrieve_audio(void* d, uint32_t size, uint32_t client_id) {
	custom_log("retrieve_audio: " + to_string(client_id), Debug, Color::Yellow);

	// Get the receiver belonging to the connected peer
	ClientReceiver* c = find_or_add_receiver(client_id);

	// Fill the allocated memory with the requested data, if possible
	int local_size = c->data_parser.fill_data_array(d, size);
	if (local_size == 0) {
		custom_log("retrieve_audio: ERROR: the audio could not be retrieved", Default, Color::Red);
	}
	else if (local_size != size) {
		custom_log("retrieve_audio: ERROR: retrieve_audio parameter size " + to_string(size) +
			" does not match the registered data length" + to_string(local_size), Default, Color::Red);
	}
	else {
		custom_log("retrieve_audio: Audio frame from client " + to_string(client_id) +
			" successfully retrieved", Debug, Color::Yellow);
	}
}

/*
	The functions below can be used to send and retrieve control messages, yet this feature is currently not supported
	by the Golang peers and the SFU.
*/

int send_control(void* data, uint32_t size) {
	if (size > BUFLEN - sizeof(PacketType)) {
		custom_log("send_control: ERROR: send_control returns -1 since the message size is too large", Default,
			Color::Red);
		return -1;
	}
	char* temp_d = reinterpret_cast<char*>(data);
	int size_sent = send_packet(temp_d, size, 2);
	return size_sent;
}

int get_control_size() {
	unique_lock<mutex> guard(m_recv_control);
	if (recv_controls.empty()) {
		guard.unlock();
		return -1;
	}
	int size = (int)recv_controls.front().get_data_length();
	guard.unlock();
	return size;
}

void retrieve_control(void* d, uint32_t size) {
	unique_lock<mutex> guard(m_recv_control);
	auto next_control_packet = std::move(recv_controls.front());
	recv_controls.pop();
	guard.unlock();
	uint32_t local_size = (uint32_t)next_control_packet.get_data_length();
	if (size != local_size) {
		custom_log("retrieve_control: ERROR: retrieve_control parameter size " + to_string(size) +
			" does not match data length " + to_string(local_size), Default, Color::Red);
		return;
	}
	char* p = next_control_packet.get_data();
	char* temp_d = reinterpret_cast<char*>(d);
	memcpy(temp_d, p, size);
}

/*
	This function allows to send out an audio frame to the Golang peer. It returns the amount of bytes sent.
*/
int send_control_packet(void* data, uint32_t size) {
	custom_log("send_control_packet: Size " + to_string(size), Debug, Color::Green);

	if (!initialized) {
		custom_log("send_tile: ERROR: The DLL has not yet been initialized!", Default, Color::Red);
		return -1;
	}

	// Required parameters
	int full_size_sent = 0;
	char* temp_d = reinterpret_cast<char*>(data);

	// Make sure only one process is sending out packets
	unique_lock<mutex> guard(m_send_data);

	// Send out packets as long as needed
	// Determine the amount of bytes to send out

	// Insert all data into a buffer
	char buf_msg[BUFLEN];
	memcpy(buf_msg, reinterpret_cast<char*>(data), size);

	// Send out the packet
	int size_sent = send_packet(buf_msg, size, PacketType::ControlPacket);
	if (size_sent < 0) {
		guard.unlock();
		custom_log("send_control_packet: ERROR: the return value of send_packet should not be negative!", Default,
			Color::Red);
		return -1;
	}

	// Update parameters
	full_size_sent += size_sent;
		
	

	custom_log("send_control_packet: Sent out control frame  " + to_string(full_size_sent) + " bytes", Debug, Color::Green);
	// Release the mutex
	guard.unlock();

	// Return the amount of bytes sent
	return full_size_sent;
}




void wait_for_peer() {
	unique_lock<mutex> lk(m_peer_ready);
	cv_peer_ready.wait(lk, [] { return peer_ready; });
}