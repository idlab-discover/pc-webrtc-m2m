#pragma once
#include <librealsense2/rs.hpp>
#include "frame.hpp"

struct RS2Bounds {
    rs2::vertex min;
    rs2::vertex max;
};

class RS2Frame : public Frame {
    public:
        RS2Frame(unsigned int capturer_id, FrameMode mode, unsigned int width, unsigned height, unsigned int bpp, unsigned int stride, 
            const rs2::depth_frame& depth_frame, const rs2::video_frame& color_frame, unsigned int frame_nr,
            FrameCleanupSettings cleanup_settings) 
            : width(width), height(height), Frame(capturer_id, frame_nr) {
            switch (mode)
            {
                case FrameMode::RealData: {
                    rs2::pointcloud pc;
                    pc.map_to(color_frame);
                    points = pc.calculate(depth_frame);
                    make_color_array(bpp, stride, static_cast<const uint8_t*>(color_frame.get_data()));
                    break;
                } 
                case FrameMode::RawData: {
                    make_raw_data_arrays(depth_frame, color_frame, cleanup_settings);
                    break;
                }
                case FrameMode::Both: {
                    rs2::pointcloud pc;
                    pc.map_to(color_frame);
                    pc.calculate(depth_frame);
                    make_color_array(bpp, stride, static_cast<const uint8_t*>(color_frame.get_data()));
                    make_raw_data_arrays(depth_frame, color_frame, cleanup_settings);
                    break;
                }
                    
            }
            
        };
        ~RS2Frame() {
            if(vertices != nullptr) {
                delete[] vertices;
            }
            if(colors != nullptr) {
                delete[] colors;
            }
            
        }
        unsigned int get_frame_size() { return n_points; }
        Vertex* get_vertex_array() { return vertices; };
        Color* get_color_array() { return colors;};

        uint16_t* get_raw_depth() {return raw_depth.data();};
        uint8_t* get_raw_colors() {return raw_color.data();};
        
        unsigned int get_capture_width() { return width;};
        unsigned int get_capture_height() { return height;};
        unsigned int get_raw_n_points() {return n_points;};
    private:
        rs2::points points;
        Color* colors = nullptr;
        Vertex* vertices = nullptr;
        std::vector<uint16_t> raw_depth;
        std::vector<uint8_t> raw_color;
        unsigned int width;
        unsigned int height;
        unsigned int n_points = 0;
        void make_color_array(unsigned int bpp , unsigned int stride, const uint8_t* texture);
        void make_raw_data_arrays(
            const rs2::depth_frame& depth_frame, const rs2::video_frame& color_frame,
            FrameCleanupSettings cleanup_settings
        );
        void apply_depth_filter_to_raw(const uint16_t* rs_depth, const uint8_t* rs_color, FrameCleanupSettings cleanup_settings);
};