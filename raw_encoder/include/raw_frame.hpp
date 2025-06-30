#pragma once
#include <cstdint>

struct RawFrame {
    uint64_t timestamp;
    unsigned int capturer_id;
    unsigned int frame_nr;
    unsigned int width;
    unsigned int height;
    unsigned int n_points;
    uint16_t* depth;
    uint8_t* color;
    void* frame_pointer; // DO NOT USE OR FREE!
};