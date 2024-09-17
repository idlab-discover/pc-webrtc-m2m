#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "capturer.hpp"



class ArtificalCapturer : public Capturer {
    public:
        ArtificalCapturer(unsigned int side_size, unsigned int fps) : Capturer(fps), side_size(side_size) {
            interframe_delay = std::chrono::milliseconds(1000 / fps);
            previous_time = std::chrono::high_resolution_clock::now();
        };
        ~ArtificalCapturer() {
           // frame_buffer.stop_buffer();
        }
        CAPTURER_SETUP_CODE init();
        CAPTURER_SETUP_CODE capture_next_frame();
        Frame* poll_next_frame();
    private:
        unsigned int side_size;
        std::chrono::milliseconds interframe_delay;
        std::chrono::steady_clock::time_point previous_time;
};