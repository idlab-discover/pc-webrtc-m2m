#pragma once

#include "buffer.hpp"
#include "data_parser.hpp"
#include "tile_receiver.hpp"

#include <fstream>
#include <iostream>
#include <map>
#include <string>

class ClientReceiver {

public:
	std::vector<TileReceiver> tile_receivers;
	std::map<uint32_t, ReceivedAudio> recv_audio;
	AudioBuffer audio_buffer;

	ClientReceiver(uint32_t client_number, uint32_t n_capturers, uint32_t n_tiles) {
		this->client_number = client_number;
		tile_receivers.reserve(n_capturers);
		for (uint32_t i = 0; i < n_capturers; ++i) {
			tile_receivers.emplace_back(client_number, n_tiles);
		}
	}
	void set_current_audio(ReceivedAudio& r) {
		current_audio = std::move(r);
	}
	uint32_t get_current_audio_size() {
		return current_audio.get_frame_length();
	}

	int fill_data_array(void* d, uint32_t size) {
		uint32_t local_size = current_audio.get_frame_length();
		if (local_size != size) {
			return local_size;
		}
		char* p = current_audio.get_data();
		char* temp_d = reinterpret_cast<char*>(d);
		memcpy(temp_d, p, local_size);
		return size;
	}


	

private:
	uint32_t client_number;
	ReceivedAudio current_audio;
};
