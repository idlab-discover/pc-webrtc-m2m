#pragma once

#include <stdio.h>
#include <iostream>
// Never try something fancy like adding virtual methods or inheritence to these structs as it adds an hidden field to the struct
// making byte copying unreliable
struct PacketType {
	uint32_t type;

	static constexpr auto size() {
		return sizeof(struct PacketType);
	}

	PacketType(char** buf, size_t& avail) {
		std::memcpy(data(), *buf, size());
		*buf += size();
		avail -= size();
	}

	char* data() {
		return reinterpret_cast<char*>(this);
	}

	std::string string_representation() {
		return "Packet type: " + std::to_string(type);
	}

	enum Type {
		ReadyPacket = 0,
		TilePacket = 1,
		AudioPacket = 2,
		ControlPacket = 3,
		TrackStatusPacket = 4,
		NumTilePacket = 5
	};
};

struct PacketHeader {
	uint32_t client_id;
	uint32_t frame_number;
	uint32_t file_length;
	uint32_t file_offset;
	uint32_t packet_length;
	uint32_t tile_id;


	static constexpr auto size() {
		return sizeof(struct PacketHeader);
	}

	PacketHeader(uint32_t client_id, uint32_t frame_number,
		uint32_t file_length, uint32_t file_offset, uint32_t packet_length, uint32_t tile_id) {
		this->client_id = client_id;
		this->frame_number = frame_number;
		this->file_length = file_length;
		this->file_offset = file_offset;
		this->packet_length = packet_length;
		this->tile_id = tile_id;
	}

	PacketHeader(char** buf, size_t& avail) {
		std::memcpy(data(), *buf, size());
		*buf += size();
		avail -= size();
	}

	char* data() {
		return reinterpret_cast<char*>(this);
	}

	std::string string_representation() {
		return "[VIDEO] Client ID: " +
			std::to_string(client_id) +
			", frame number: " +
			std::to_string(frame_number) +
			", file length: " +
			std::to_string(file_length) +
			", file offset: " +
			std::to_string(file_offset) +
			", packet length: " +
			std::to_string(packet_length);
	}
protected:
	PacketHeader() {};
};

struct AudioPacketHeader {
	uint32_t client_id;
	uint32_t frame_number;
	uint32_t file_length;
	uint32_t file_offset;
	uint32_t packet_length;


	static constexpr auto size() {
		return sizeof(struct AudioPacketHeader);
	}

	AudioPacketHeader(uint32_t client_id, uint32_t frame_number,
		uint32_t file_length, uint32_t file_offset, uint32_t packet_length) {
		this->client_id = client_id;
		this->frame_number = frame_number;
		this->file_length = file_length;
		this->file_offset = file_offset;
		this->packet_length = packet_length;
	}

	AudioPacketHeader(char** buf, size_t& avail) {
		std::memcpy(data(), *buf, size());
		*buf += size();
		avail -= size();
	}

	char* data() {
		return reinterpret_cast<char*>(this);
	}

	std::string string_representation() {
		return "[AUDIO] Client ID: " +
			std::to_string(client_id) +
			", frame number: " +
			std::to_string(frame_number) +
			", file length: " +
			std::to_string(file_length) +
			", file offset: " +
			std::to_string(file_offset) +
			", packet length: " +
			std::to_string(packet_length);
	}
protected:
	AudioPacketHeader() {};
};

struct TrackStatusChangedHeader {
	uint32_t client_id;
	uint32_t last_frame_nr;
	uint32_t tile_nr;
	bool is_video;
	bool is_added;

	static constexpr auto size() {
		return sizeof(struct TrackStatusChangedHeader);
	}

	TrackStatusChangedHeader(char** buf, size_t& avail) {
		std::memcpy(data(), *buf, size());
		*buf += size();
		avail -= size();
	}

	char* data() {
		return reinterpret_cast<char*>(this);
	}
};