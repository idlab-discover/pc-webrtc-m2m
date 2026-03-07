#pragma once
#include <cstdint>


struct Vertex {
    float x, y, z;
};

struct Color {
    uint8_t r, g, b;
};

#pragma pack(push, 1)
struct PlyPointRead {
    Vertex vertex;
    Color color;
};
#pragma pack(pop)

struct PlyPointWrite {
    Vertex vertex;
    Color color;
};