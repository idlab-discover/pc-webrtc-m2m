#pragma once

#include "buffer.hpp"
#include "data_parser.hpp"

#include <fstream>
#include <iostream>
#include <map>
#include <string>

class TileReceiver {

public:
	
	TileReceiver(uint32_t client_number, uint32_t n_tiles) {
		tile_buffer.set_number_of_tiles(n_tiles);
		data_parser.set_number_of_tiles(n_tiles);
	}
	TileReceiver(const TileReceiver&& other) {
		// TODO maybe fix this; shouldnt be needed tbh
	}
	
	
	std::map<std::pair<uint32_t, uint32_t>, ReceivedTile> recv_tiles;
	DataParser data_parser;
	Buffer tile_buffer;

private:
};
