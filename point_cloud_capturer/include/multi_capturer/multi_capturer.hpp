#pragma once
#include "capturer.hpp"
#include <mutex>
#include <thread>
#include <vector>

class MultiCapturer {
    public:
        MultiCapturer(uint32_t fps, 
            FrameMode mode, FrameCleanupSettings cleanup_settings, 
            CAPTURE_TYPE type, std::vector<Capturer*>&& capturers);
        virtual ~MultiCapturer();
        void start_capturing();

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
        PointCloud* poll_next_combined_point_cloud() {
          
            std::vector<PointCloud*> point_clouds;
            point_clouds.reserve(capturers.size());
            unsigned int total_points = 0;
            for (auto& capturer : capturers) {
                PointCloud* pc = capturer->poll_next_point_cloud();
                // If nullptr -> keep polling
                if (pc != nullptr) {
                    total_points += pc->n_points;
                    point_clouds.push_back(pc);
                }
            }
            // If all nullptr -> return
            // Else calculate highest timestamp
            // Poll other cameras until they get good frame with timestamp close to highest timestamp
            // Set #frames to drop (without sleep) based on lowest timestamp
            // If frameNr == 0 calculate s_seek
            if(total_points == 0) {
                return nullptr; // No point clouds captured
            }

            PointCloud* combined_pc = new PointCloud();
            combined_pc->n_points = total_points;
            combined_pc->timestamp = point_clouds.empty() ? 0 : point_clouds[0]->timestamp;
            combined_pc->coords = new Vertex[combined_pc->n_points];
            combined_pc->colors = new Color[combined_pc->n_points];
            unsigned int current_index = 0;
            for(auto& pc : point_clouds) {
                if(pc != nullptr) {
                    std::copy(pc->coords, pc->coords + pc->n_points, combined_pc->coords + current_index);
                    std::copy(pc->colors, pc->colors + pc->n_points, combined_pc->colors + current_index);
                    current_index += pc->n_points;
                    delete pc; // Free the individual point cloud
                }
            }
          
            return combined_pc;
        }

    protected:
        std::vector<Capturer*> capturers;
};