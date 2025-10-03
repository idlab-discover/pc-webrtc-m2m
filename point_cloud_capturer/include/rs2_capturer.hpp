#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "capturer.hpp"
#include <optional>

struct RS2CaptureSettings {
    unsigned int width;
    unsigned int height;
    float min_dist;
    float max_dist;
    bool align_to_depth;
};

class RS2Capturer : public Capturer {
    public:
        RS2Capturer(
            unsigned int capture_id,
            unsigned int fps, FrameMode mode,
            FrameCleanupSettings cleanup_settings,
            RS2CaptureSettings* capture_settings
        ) try : width(capture_settings->width), height(capture_settings->height), 
            min_dist(capture_settings->min_dist), 
            max_dist(capture_settings->max_dist),
            align_to_depth(capture_settings->align_to_depth), 
            Capturer(capture_id, mode, fps, cleanup_settings), 
            depth_align(rs2::align(RS2_STREAM_DEPTH)), 
            color_align(rs2::align(RS2_STREAM_COLOR)) 
        {
            
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
        void* get_calibration();
        uint32_t get_calibration_size() {
            return sizeof(RealsenseCalibration);
        }
        static void free_calibration(void* cal) {
            free_calibration_internal<RealsenseCalibration>(cal);
        };
        Frame* get_single_frame();
    private:
        rs2::pipeline pipe;
        rs2::pointcloud pc;
        rs2::align depth_align; // Do this only once because its expensive
        rs2::align color_align; // Do this only once because its expensive
        rs2::threshold_filter thres_filter;
        std::optional<rs2::depth_sensor> depth_sensor;
        std::optional<rs2::color_sensor> color_sensor;
        unsigned int width;
        unsigned int height;
        float min_dist;
        float max_dist;
        bool align_to_depth;
        std::pair<CAPTURER_SETUP_CODE, std::string> exception_handler() noexcept;
        realsense_in get_intrinsincs_from_stream(rs2::video_stream_profile profile);
};