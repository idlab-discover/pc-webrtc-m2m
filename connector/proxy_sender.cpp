#include "pch.h"
#include "data_parser.hpp"
#include "debug.h"
#include "frame_buffer.hpp"
#include "framework.h"
#include "packet_data.hpp"
#include "plugin.h"
#include "received_control_packet.hpp"

#include <chrono>
#include <fstream>
#include <iostream>
#include <string>
#include <thread>

#define BUFLEN 1300	// Maximum length of buffer

using namespace std;

static struct sockaddr_in si_send;
static int s_send, slen = sizeof(si_send);

static char* buf = (char*)malloc(BUFLEN);
static char* buf_ori = buf;

static WSADATA wsa;
static std::thread worker;
static bool keep_working = true;

static std::map<uint32_t, ReceivedFrame> recv_frames;
static std::queue<ReceivedControlPacket> recv_control_packets;
static DataParser data_parser;
static FrameBuffer frame_buffer;
static int frame_counter = 0;
std::mutex m_recv_control;

static string logfile = "log.txt";

enum CONNECTION_SETUP_CODE : int {
	ConnectionSuccess = 0,
	StartUpError = 1,
	SocketCreationError = 2,
	SendToError = 3
};

struct SendPacketHeader {
	uint32_t framenr;
	uint32_t framelen;
	uint32_t frameoffset;
	uint32_t packetlen;
};

inline string get_current_data_time() {
	time_t now = time(0);
	char buf[80];
	struct tm tstruct;
	localtime_s(&tstruct, &now);
	strftime(buf, sizeof(buf), "%Y-%m-%d %X", &tstruct);
	return string(buf);
};

inline void log(string message) {
	string filePath = "C:\\Users\\jeroe\\GitHub\\cwipc_test\\cwipc-unity-test\\Assets\\Plugins\\" + logfile;
	ofstream ofs(filePath.c_str(), std::ios_base::out | std::ios_base::app);
	ofs << get_current_data_time() << '\t' << message << '\n';
	ofs.close();
}

void set_logfile(char* _logfile) {
	Debug::log("Setting log file to " + string(_logfile), Color::Blue);
	logfile = _logfile;
}

int setup_connection(char* server, uint32_t port) {
	Debug::log("Connecting to " + string(server), Color::Blue);
	if (WSAStartup(MAKEWORD(2, 2), &wsa) != 0) {
		return StartUpError;
	}
	if ((s_send = socket(AF_INET, SOCK_DGRAM, IPPROTO_UDP)) == SOCKET_ERROR) {
		WSACleanup();
		return SocketCreationError;
	}
	ULONG buf_size = 524288000;
	setsockopt(s_send, SOL_SOCKET, SO_RCVBUF, (char*)&buf_size, sizeof(ULONG));
	si_send.sin_family = AF_INET;
	si_send.sin_port = htons(port);
	inet_pton(AF_INET, server, &si_send.sin_addr.S_un.S_addr);
	char t[BUFLEN] = { 0 };
	t[0] = 'a';
	if (sendto(s_send, t, BUFLEN, 0, (struct sockaddr*)&si_send, slen) == SOCKET_ERROR) {
		WSACleanup();
		return SendToError;
	}
	return ConnectionSuccess;
}

void listen_work() {
	Debug::log("Start listening thread", Color::Blue);

	// TODO: add poll for performance, maybe
	while (keep_working) {
		size_t size = 0;
		if ((size = recvfrom(s_send, buf, BUFLEN, 0, (struct sockaddr*)&si_send, &slen)) == SOCKET_ERROR) {
			printf("recvfrom() failed with error code : %d", WSAGetLastError());
			exit(EXIT_FAILURE);
		}
		struct PacketType p_type(&buf, size);
		if (p_type.type == 0) {
			struct PacketHeader p_header(&buf, size);
			auto frame = recv_frames.find(p_header.framenr);
			if (frame == recv_frames.end()) {
				auto e = recv_frames.emplace(p_header.framenr, ReceivedFrame(p_header.framelen, p_header.framenr));
				frame = e.first;
			}
			frame->second.insert(buf, p_header.frameoffset, p_header.packetlen, size);
			if (frame->second.is_complete()) {
				log("frame is now complete");
				frame_buffer.insert_frame(frame->second);
				recv_frames.erase(p_header.framenr);
				log("new frame buffer size is " + to_string(frame_buffer.get_buffer_size()));
			}
		} else if (p_type.type == 1) {
			recv_control_packets.push(ReceivedControlPacket(buf, size));
		}
		buf = buf_ori;
		// std::this_thread::sleep_for(std::chrono::milliseconds(1));
	}
	log("listen_work terminating");
}

void start_listening() {
	log("start_listening called");
	worker = std::thread(listen_work);
	log("start_listening terminating");
}

int next_frame() {
	log("next_frame called");
	if (frame_buffer.get_buffer_size() == 0) {
		log("next_frame terminating while returning -1");
		return -1;
	}
	ReceivedFrame f = frame_buffer.next();
	data_parser.set_current_frame(f);
	int frame_size = data_parser.get_current_frame_size();
	log("next_frame terminating while returning " + to_string(frame_size) + " bytes");
	return frame_size;
}

void set_frame_data(void* d) {
	log("set_frame_data called");
	data_parser.fill_data_array(d);
	log("clean_up terminating");
}

int next_control_packet() {
	std::unique_lock<std::mutex> guard(m_recv_control);
	if (recv_control_packets.empty()) {
		return -1;
	}
	int size = recv_control_packets.front().get_data_length();
	guard.unlock();
	return size;
}

void set_control_data(void* d) {
	std::unique_lock<std::mutex> guard(m_recv_control);
	auto next_control_packet = std::move(recv_control_packets.front());
	recv_control_packets.pop();
	guard.unlock();
	char* p = next_control_packet.get_data();
	uint32_t framelen = next_control_packet.get_data_length();
	char* temp_d = reinterpret_cast<char*>(d);
	memcpy(temp_d, p, framelen);
}

void clean_up() {
	log("clean_up called");
	keep_working = false;
	if (worker.joinable())
		worker.join();
	// WSACleanup();
	// TODO: check if socket should be closed
	free(buf);
	log("clean_up terminating");
}

int send_packet(char* data, uint32_t size, uint32_t _packet_type) {
	log("send_packet called");
	uint32_t packet_type = _packet_type;
	int size_send = 0;
	char buf_msg[BUFLEN] = { 0 };
	memcpy(buf_msg, &packet_type, size);
	memcpy(&buf_msg[sizeof(packet_type)], data, size);
	if ((size_send = sendto(s_send, buf_msg, BUFLEN, 0, (struct sockaddr*)&si_send, slen)) == SOCKET_ERROR) {
		return -1;
	}
	log("send_packet terminating while sending out " + to_string(size_send) + " bytes");
	return size_send;
}

int send_frame_data(void* data, uint32_t size) {
	log("send_frame_data called");
	uint32_t buflen_nheader = BUFLEN - 4 - sizeof(SendPacketHeader);
	uint32_t current_offset = 0;
	uint32_t remaining = size;
	int full_size_send = 0;
	char* temp_d = reinterpret_cast<char*>(data);
	while (remaining > 0) {
		uint32_t next_size = 0;
		if (remaining >= buflen_nheader) {
			next_size = buflen_nheader;
		} else {
			next_size = remaining;
		}
		char buf_msg[BUFLEN];
		struct SendPacketHeader p {
			frame_counter, size, current_offset, next_size
		};
		memcpy(buf_msg, &p, sizeof(p));
		memcpy(buf_msg + sizeof(p), reinterpret_cast<char*>(data) + current_offset, next_size);
		int size_send = send_packet(buf_msg, next_size + sizeof(SendPacketHeader), 1);
		if (size_send == SOCKET_ERROR) {
			return size_send;
		}
		full_size_send += size_send;
		current_offset += next_size;
		remaining -= next_size;
	}
	frame_counter++;
	log("send_frame_data terminating while sending out " + to_string(full_size_send) + " bytes");
	return full_size_send;
}

int send_control_data(void* data, uint32_t size) {
	if (size > BUFLEN - 4) {
		return -1;
	}
	char* temp_d = reinterpret_cast<char*>(data);
	return send_packet(temp_d, size, 1);
}
