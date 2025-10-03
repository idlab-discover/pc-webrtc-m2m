#pragma once
#include "capturer.hpp"
#include <mutex>
#include <thread>
#include <vector>
#include "log.h"

class MultiCapturer {
    public:
        MultiCapturer(uint32_t fps, 
            FrameMode mode, FrameCleanupSettings cleanup_settings, 
            CAPTURE_TYPE type, std::vector<Capturer*>&& capturers);
        virtual ~MultiCapturer();
        void start_capturing(bool start_capture_thread);

        // Single capturer functions
        Frame* poll_next_frame_for_capturer(unsigned int capturer_index);
        PointCloud* poll_next_point_cloud_for_capturer(unsigned int capturer_index);
        RawFrame* poll_next_raw_frame_for_capturer(unsigned int capturer_index);
        void* get_calibration_for_capturer(unsigned int capturer_index);
        uint32_t get_calibration_size_for_capturer(unsigned int capturer_index);
        void set_cleanup_settings_for_capturer(unsigned int capturerer_index, FrameCleanupSettings _cleanup_settings);
        bool register_frame_ready_callback_for_capturer(unsigned int capturer_index, FrameReadyCallback cb);
        // Combined functions
        // TODO improve this to remove duplicate points and probably reuse a combined point cloud
        PointCloud* get_single_combined_point_cloud();
        PointCloud* poll_next_combined_point_cloud();

    protected:
        std::vector<Capturer*> capturers;
        unsigned int current_frame_nr = 0; // TODO calculate this based on timestamp from cameras
    private:
        PointCloud* combine_point_clouds(unsigned int total_points, const std::vector<PointCloud*>& point_clouds);
};