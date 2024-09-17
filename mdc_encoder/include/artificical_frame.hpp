#pragma once
#include <librealsense2/rs.hpp>
#include "point_cloud_data.h"
#include "frame.hpp"
class ArtificalFrame : public Frame {
    public:
        ArtificalFrame(unsigned int side_size, unsigned int _frame_nr) : Frame(_frame_nr) {
            make_data_arrays(side_size);

        };
        ~ArtificalFrame() {
            delete[] points;
            delete[] colors;
        }
        unsigned int get_frame_size() { return n_points; }
        Vertex* get_vertex_array() { return points; };
        Color* get_color_array() { return colors;};
      
    private:
        Vertex* points = nullptr; 
        Color* colors = nullptr;
        unsigned int n_points = 0;
        void make_data_arrays(unsigned int side_size);
};