#include "frame.hpp"
#include "point_cloud.hpp"
#include "raw_frame.hpp"

PointCloud* Frame::get_point_cloud() {
    return new PointCloud{
        get_timestamp(),
        get_capturer_id(),
        get_frame_nr(),
        get_frame_size(),
        get_vertex_array(),
        get_color_array(),
        this
    };
}

RawFrame* Frame::get_raw_frame() {
    return new RawFrame{
        get_timestamp(),
        get_capturer_id(),
        get_frame_nr(),
        get_capture_width(),
        get_capture_height(),
        get_raw_n_points(),
        get_raw_depth(),
        get_raw_colors(),
        this
    };
}