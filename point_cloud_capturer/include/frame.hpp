#pragma once
#include <vector>
#include <chrono>
#include "point_cloud_data.h"
class PointCloud;
struct Point {
    float x, y, z;
    uint8_t r, g, b;
};
enum FrameMode {
    RealData = 0,
    RawData = 1,
    Both = 2,
};

struct FrameCleanupSettings {
    unsigned int blackout_block_size;
    bool should_apply_depth_filter;
    bool should_cleanup_depth;
    bool should_blackout;
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
        virtual uint16_t* get_raw_depth() = 0;
        virtual uint8_t* get_raw_colors() = 0;
        virtual unsigned int get_capture_width() = 0;
        virtual unsigned int get_capture_height() = 0;
        virtual unsigned int get_raw_n_points() = 0;

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