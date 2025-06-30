#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "capturer.hpp"
struct ArtificalCaptureSettings {
    unsigned int side_size;
};
class ArtificalCapturer : public Capturer {
    public:
        ArtificalCapturer(
            unsigned int capture_id,
            unsigned int fps, FrameMode mode, FrameCleanupSettings cleanup_settings,
            ArtificalCaptureSettings* capture_settings
        ) : Capturer(capture_id, mode, fps, cleanup_settings), side_size(capture_settings->side_size) 
        {
            interframe_delay = std::chrono::milliseconds(1000 / fps);
            previous_time = std::chrono::high_resolution_clock::now();
        };
        ~ArtificalCapturer() {
           // frame_buffer.stop_buffer();
        }
        CAPTURER_SETUP_CODE init();
        CAPTURER_SETUP_CODE capture_next_frame();
        Frame* poll_next_frame();
        void* get_calibration();
        static void free_calibration(void* cal) {
            free_calibration_internal<ArtificialCalibration>(cal);
        };
    private:
        unsigned int side_size;
        std::chrono::milliseconds interframe_delay;
        std::chrono::steady_clock::time_point previous_time;
};