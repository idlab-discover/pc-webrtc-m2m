#pragma once

#include "point_cloud_data.h"
#include "frame.hpp"
class ArtificalFrame : public Frame {
    public:
        ArtificalFrame(unsigned int capturer_id, FrameMode mode, unsigned int side_size, unsigned int _frame_nr) 
                : side_size(side_size), Frame(capturer_id, _frame_nr) {
            switch (mode)
            {
            case FrameMode::RealData:
                make_data_arrays(side_size);
                break;
                make_raw_data_arrays(side_size);
            case FrameMode::RawData:
                make_raw_data_arrays(side_size);
                break;
            case FrameMode::Both:
                make_raw_data_arrays(side_size);
                make_data_arrays(side_size);
                break;
            }
           

        };
        ~ArtificalFrame() {
            if(points != nullptr) {
                delete[] points;
            }
            if(colors != nullptr) {
                delete[] colors;
            }
            
        }
        unsigned int get_frame_size() { return n_points; }
        Vertex* get_vertex_array() { return points; };
        Color* get_color_array() { return colors;};
        uint16_t* get_raw_depth() { return raw_depth.data(); };
        uint8_t* get_raw_colors() { return raw_color.data(); };

        unsigned int get_capture_width() { return side_size;};
        unsigned int get_capture_height() { return side_size;};
        unsigned int get_raw_n_points() {return n_points;};
      
    private:
        Vertex* points = nullptr; 
        Color* colors = nullptr;
        std::vector<uint16_t> raw_depth;
        std::vector<uint8_t> raw_color;
        unsigned int n_points = 0;
        unsigned int side_size;
        void make_data_arrays(unsigned int side_size);
        void make_raw_data_arrays(unsigned int side_size);

};