#pragma once
#include <vector>
#include <chrono>
#include "point_cloud_data.h"
#include <string>
class PointCloud;
class RawFrame; 
struct Point {
    float x, y, z;
    uint8_t r, g, b;
};
enum FrameMode {
    RealData = 0,
    RawData = 1,
    Both = 2,
};

//#pragma pack(push, 1)
struct FrameCleanupSettings {
    unsigned int blackout_block_size;
    bool should_apply_depth_filter;
    bool should_cleanup_depth;
    bool should_blackout;
};
//#pragma pack(pop)


class Frame {
    public:
        Frame(unsigned int capturer_id, unsigned int frame_nr) : capturer_id(capturer_id), frame_nr(frame_nr) {
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

        unsigned int get_capturer_id() const { return capturer_id; };
        unsigned int get_frame_nr() const {return frame_nr;};
        float get_x_offset() const {return x_offset; };
        float get_y_offset() const {return y_offset; };
        float get_z_offset() const {return z_offset; };
        uint64_t get_timestamp() const {return timestamp; };
        uint64_t get_device_timestamp() const { return device_timestamp; };

        PointCloud* get_point_cloud();
        RawFrame* get_raw_frame();
        bool is_valid_frame() const { return is_valid; }
    protected:
        unsigned int capturer_id; // ID of the capturer that created this frame
        unsigned int frame_nr;
        float x_offset = 0.0; 
        float y_offset = 0.0;
        float z_offset = 0.0;
        uint64_t timestamp;
        int64_t device_timestamp = -1; // Device timestamp, if applicable
        bool is_valid = false;
};