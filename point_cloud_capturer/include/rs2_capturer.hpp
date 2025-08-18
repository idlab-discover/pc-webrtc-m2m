#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "capturer.hpp"



class RS2Capturer : public Capturer {
    public:
        RS2Capturer(unsigned int width, unsigned int height, unsigned int fps, float min_dist, float max_dist) try : width(width), height(height), min_dist(min_dist), max_dist(max_dist), Capturer(fps),depth_align(rs2::align((RS2_STREAM_DEPTH))) {

        } catch(...) {
            auto e = exception_handler();
            throw e.first;
        };
        ~RS2Capturer() {
            pipe.stop();
           // frame_buffer.stop_buffer();
        }
        CAPTURER_SETUP_CODE init();
        CAPTURER_SETUP_CODE capture_next_frame();
        Frame* poll_next_frame();
    private:
        rs2::pipeline pipe;
        
        rs2::align depth_align; // Do this only once because its expensive
        rs2::threshold_filter thres_filter;
        unsigned int width;
        unsigned int height;
        float min_dist;
        float max_dist;
        std::pair<CAPTURER_SETUP_CODE, std::string> exception_handler() noexcept;
};