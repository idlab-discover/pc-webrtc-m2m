#pragma once

#include "received_audio.hpp"
#include <list>
#include <mutex>
#include <condition_variable>

class AudioBuffer {

public:
	AudioBuffer() {
		frame_number = 0;
	}
	~AudioBuffer() {
		cv.notify_all();
	}
	void set_number_of_tiles(uint32_t number_of_tiles) {
		frame_number = 0;
	}


	ReceivedAudio next() {
		std::unique_lock<std::mutex> guard(m);
		
		ReceivedAudio next_audio_frame = audio_queue.top();
		audio_queue.pop();
		while (audio_queue.size() > 100) {
			next_audio_frame = audio_queue.top();
			audio_queue.pop();
		}
		frame_number = next_audio_frame.get_frame_number();
		guard.unlock();
	
		return std::move(next_audio_frame);
	}

	bool insert_audio_frame(ReceivedAudio& audio_frame) {
		std::unique_lock<std::mutex> guard(m);
		uint32_t new_frame_number = audio_frame.get_frame_number();
		if (new_frame_number < frame_number) {
			guard.unlock();
			return false;
		}
		audio_queue.push(std::move(audio_frame));
		guard.unlock();
		cv.notify_one();
		// frame_numbers[tile_number] = frame_number;

		return true;
	}
	size_t get_buffer_size() {;
		return audio_queue.size();
	}

	std::priority_queue<ReceivedAudio> get_queue() {
		return audio_queue;
	}

	bool wait_for_audio() {
		std::unique_lock<std::mutex> guard(m);

		cv.wait(guard, [this] { return stop_waiting || audio_queue.size() > 0; });
		guard.unlock();
		if (stop_waiting) {
			return false;
		}
		return true;
	}

private:
	std::priority_queue<ReceivedAudio> audio_queue;
	std::condition_variable cv;
	uint32_t frame_number;
	std::mutex m;
	bool stop_waiting = false;
};
