#pragma once
#include "point_cloud_data.h"
#include "frame.hpp"


class PlyFrame : public Frame {
    public:
        PlyFrame(unsigned int capturer_id, FrameMode mode, const std::string& file_path, unsigned int _frame_nr) 
                : Frame(capturer_id, _frame_nr) {
            //switch (mode)
            //{
            //case FrameMode::RealData:
                make_data_arrays(file_path);
                timestamp = std::chrono::time_point_cast<std::chrono::milliseconds>(std::chrono::system_clock::now()).time_since_epoch().count();
            //    break;
            //}
           

        };
        ~PlyFrame() {
               
        }
        unsigned int get_frame_size() { return n_points; }
        Vertex* get_vertex_array() { return vertices.data(); };
        Color* get_color_array() { return colors.data();};
        uint16_t* get_raw_depth() { return nullptr; };
        uint8_t* get_raw_colors() { return raw_color.data(); };

        unsigned int get_capture_width() { return 0;};
        unsigned int get_capture_height() { return 0;};
        unsigned int get_raw_n_points() {return n_points;};
      
    private:
        std::vector<Vertex> vertices;
        std::vector<Color> colors;
        std::vector<uint16_t> raw_depth;
        std::vector<uint8_t> raw_color;
        unsigned int n_points = 0;
        void make_data_arrays(const std::string& file_path);

};