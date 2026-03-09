#pragma once

#include <cstdint>
#include <iostream>
#include <queue>
#include <stdio.h>
#include <string>
#include <cstring>

class ReceivedAudio {

public:
	ReceivedAudio() {}

	ReceivedAudio(uint32_t _frame_length, uint32_t _frame_number) {
		frame_length = _frame_length;
		frame_number = _frame_number;
		current_size = 0;
		data.resize(frame_length);
	}

	~ReceivedAudio() {}

	bool insert(char* b, uint32_t frameoffset, uint32_t len, size_t tt) {
		if (len == 0) {
			return true;
		}
		std::memcpy(&data[frameoffset], b, len);
		current_size += len;
		return true;
	}

	bool is_complete() {
		return current_size == frame_length;
	}

	uint32_t get_frame_number() const {
		return frame_number;
	}


	uint32_t get_frame_length() {
		return frame_length;
	}

	uint32_t get_current_size() {
		return current_size;
	}

	char* get_data() {
		return data.data();
	}

	size_t get_data_length() {
		return data.size();
	}

	std::vector<char> get_data_v() {
		return data;
	}

	uint8_t get_temp() {
		return temp;
	}

	std::string print() {
		return "TL = " + std::to_string(frame_length) + ", FN = " + std::to_string(frame_number) + ", CS = " + std::to_string(current_size) + ", DS = " +
			std::to_string(data.size());
	}

private:
	uint32_t frame_length = 0;
	uint32_t frame_number = 0;
	uint32_t current_size = 0;
	std::vector<char> data;
	uint8_t temp = 0;
};

bool operator<(const ReceivedAudio& f1, const ReceivedAudio& f2) {
	return f1.get_frame_number() > f2.get_frame_number() || (f1.get_frame_number() == f2.get_frame_number());
}
