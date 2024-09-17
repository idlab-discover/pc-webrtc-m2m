#pragma once
#include <librealsense2/rs.hpp>
#include "frame.hpp"

struct RS2Bounds {
    rs2::vertex min;
    rs2::vertex max;
};

class RS2Frame : public Frame {
    public:
        RS2Frame(unsigned int width, unsigned height, unsigned int bpp , unsigned int stride, rs2::points points, const uint8_t* texture, unsigned int frame_nr) : Frame(frame_nr), points(points) {
            make_color_array(width, height, bpp, stride, texture);
        };
        ~RS2Frame() {
            delete[] colors;
        }
        unsigned int get_frame_size() { return points.size(); }
        Vertex* get_vertex_array() { return const_cast<Vertex*>(reinterpret_cast<const Vertex*>(points.get_vertices())); };
        Color* get_color_array() { return colors;};
        
    private:
        rs2::points points;
        Color* colors = nullptr;

        void make_color_array(unsigned int width, unsigned height, unsigned int bpp , unsigned int stride, const uint8_t* texture);
};