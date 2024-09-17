#pragma once
#include <vector>
#include "point_cloud_data.h"
class PointCloud;
struct Point {
    float x, y, z;
    uint8_t r, g, b;
};

class Frame {
    public:
        Frame(unsigned int frame_nr) : frame_nr(frame_nr) {};
        virtual ~Frame() = default;
        virtual unsigned int get_frame_size() = 0;
        virtual Vertex* get_vertex_array() = 0;
        virtual Color* get_color_array() = 0;

        unsigned int get_frame_nr() {return frame_nr;};
        float get_x_offset() {return x_offset; };
        float get_y_offset() {return y_offset; };
        float get_z_offset() {return z_offset; };
    protected:
        unsigned int frame_nr;
        float x_offset = 0.0; 
        float y_offset = 0.0;
        float z_offset = 0.0;
};