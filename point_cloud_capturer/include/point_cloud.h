#pragma once
#include <cstdint>
struct Vertex {
    float x, y, z;
};
struct Color {
    uint8_t r, g, b;
};
struct PointCloud {
    unsigned int frame_nr;
    unsigned int n_points;
    float x_offset;
    float y_offset;
    float z_offset;
    Vertex* coords;
    Color* colors;
    void* frame_pointer; // DO NOT USE OR FREE!

  
};