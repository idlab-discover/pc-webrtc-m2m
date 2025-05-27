#pragma once
#include "capturer.hpp"
#include <mutex>
#include <thread>
#include <vector>

class MultiCapturer {
    public:
        MultiCapturer(uint32_t fps, 
            FrameMode mode, FrameCleanupSettings cleanup_settings, 
            CAPTURE_TYPE type, unsigned int n_settings, void* capture_settings);
        virtual ~MultiCapturer();
        void start_capturing();

        // Single capturer functions
        Frame* poll_next_frame_for_capturer(unsigned int capturer_index);
        PointCloud* poll_next_point_cloud_for_capturer(unsigned int capturer_index);
        void* get_calibration_for_capturer(unsigned int capturer_index);
        void set_cleanup_settings_for_capturer(unsigned int capturerer_index, FrameCleanupSettings _cleanup_settings);

        // Combined functions
        // TODO improve this to remove duplicate points and probably reuse a combined point cloud
        PointCloud* poll_next_combined_point_cloud() {
          
            std::vector<PointCloud*> point_clouds;
            point_clouds.reserve(capturers.size());
            unsigned int total_points = 0;
            for (auto& capturer : capturers) {
                PointCloud* pc = capturer->poll_next_point_cloud();
                if (pc != nullptr) {
                    total_points += pc->n_points;
                    point_clouds.push_back(pc);
                }
            }
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