#pragma once
#include <cstdint>
#include "frame.hpp"
struct RawFrame {
    uint64_t timestamp;
    unsigned int capturer_id;
    unsigned int frame_nr;
    unsigned int width;
    unsigned int height;
    unsigned int n_points;
    uint16_t* depth;
    uint8_t* color;
    Frame* frame_pointer = nullptr; // DO NOT USE OR FREE YOURSELF!

    ~RawFrame() {
        delete frame_pointer;
    };
};