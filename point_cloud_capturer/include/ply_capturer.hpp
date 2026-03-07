#pragma once
#include "framework.h"
#include <string>
#include "capturer.hpp"
struct PlyCaptureSettings {
    char directory_path[256];
};
class PlyCapturer : public Capturer {
    public:
        PlyCapturer(
            unsigned int capture_id,
            unsigned int fps, FrameMode mode, FrameCleanupSettings cleanup_settings,
            PlyCaptureSettings* capture_settings
        ) : Capturer(capture_id, mode, fps, cleanup_settings),
            directory_path(std::string(capture_settings->directory_path))
        {
            interframe_delay = std::chrono::milliseconds(1000 / fps);
            previous_time = std::chrono::steady_clock::now();
        };
        ~PlyCapturer() {
           // frame_buffer.stop_buffer();
        }
        CAPTURER_SETUP_CODE init();
        CAPTURER_SETUP_CODE capture_next_frame();
        Frame* poll_next_frame();
        void* get_calibration();
        uint32_t get_calibration_size() {
            return sizeof(ArtificialCalibration);
        }
        static void free_calibration(void* cal) {
            free_calibration_internal<ArtificialCalibration>(cal);
        };
        Frame* get_single_frame();
    private:
        unsigned int current_file_index = 0;
        const std::string directory_path;
        std::vector<std::string> ply_files = {};
        std::chrono::milliseconds interframe_delay;
        std::chrono::steady_clock::time_point previous_time;
};