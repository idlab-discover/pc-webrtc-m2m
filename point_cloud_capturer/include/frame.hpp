#pragma once
#include <vector>
#include <chrono>
#include "point_cloud_data.h"
class PointCloud;
struct Point {
    float x, y, z;
    uint8_t r, g, b;
};

class Frame {
    public:
        Frame(unsigned int frame_nr) : frame_nr(frame_nr) {
            timestamp = std::chrono::time_point_cast<std::chrono::milliseconds>(std::chrono::system_clock::now()).time_since_epoch().count();
        };
        virtual ~Frame() = default;
        virtual unsigned int get_frame_size() = 0;
        virtual Vertex* get_vertex_array() = 0;
        virtual Color* get_color_array() = 0;

        unsigned int get_frame_nr() {return frame_nr;};
        float get_x_offset() {return x_offset; };
        float get_y_offset() {return y_offset; };
        float get_z_offset() {return z_offset; };
        uint64_t get_timestamp() {return timestamp; };
    protected:
        unsigned int frame_nr;
        float x_offset = 0.0; 
        float y_offset = 0.0;
        float z_offset = 0.0;
        uint64_t timestamp;
};