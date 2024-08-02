#pragma once

#include "received_tile.hpp"
#include <list>

class Buffer {

public:
	Buffer() {}

	void set_number_of_tiles(uint32_t number_of_tiles) {
		tile_queues = std::vector<std::priority_queue<ReceivedTile>>(number_of_tiles);
		frame_numbers = std::vector<uint32_t>(number_of_tiles, 0);
	}

	uint32_t get_number_of_tiles() {
		return (uint32_t)tile_queues.size();
	}

	ReceivedTile next(uint32_t tile_number) {
		std::unique_lock<std::mutex> guard(m);
		ReceivedTile next_tile = tile_queues[tile_number].top();
		tile_queues[tile_number].pop();
		while (!tile_queues[tile_number].empty()) {
			next_tile = tile_queues[tile_number].top();
			tile_queues[tile_number].pop();
		}
		frame_numbers[tile_number] = next_tile.get_frame_number();
		guard.unlock();
		return std::move(next_tile);
	}

	bool insert_tile(ReceivedTile& tile, uint32_t tile_number) {
		std::unique_lock<std::mutex> guard(m);
		uint32_t frame_number = tile.get_frame_number();
		if (frame_number < frame_numbers[tile_number]) {
			guard.unlock();
			return false;
		}
		while (!tile_queues[tile_number].empty()) {
			tile_queues[tile_number].pop();
		}
		tile_queues[tile_number].push(std::move(tile));
		// frame_numbers[tile_number] = frame_number;
		guard.unlock();
		return true;
	}

	size_t get_buffer_size(uint32_t tile_number) {
		std::unique_lock<std::mutex> guard(m);
		size_t size = tile_queues[tile_number].size();
		guard.unlock();
		return size;
	}

	std::priority_queue<ReceivedTile> get_queue(uint32_t tile_number) {
		std::unique_lock<std::mutex> guard(m);
		std::priority_queue<ReceivedTile> q = tile_queues[tile_number];
		guard.unlock();
		return q;
	}

private:
	std::vector<std::priority_queue<ReceivedTile>> tile_queues;
	std::vector<uint32_t> frame_numbers;
	std::mutex m;
};
