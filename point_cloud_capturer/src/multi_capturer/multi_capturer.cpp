#include "multi_capturer/multi_capturer.hpp"
#include "multi_capturer.hpp"

MultiCapturer::MultiCapturer(uint32_t fps, 
    FrameMode mode, FrameCleanupSettings cleanup_settings, 
    CAPTURE_TYPE type, std::vector<Capturer*>&& capturers) : capturers(capturers){
    // Constructor implementation (if needed)
}

MultiCapturer::~MultiCapturer() {
    for (auto& capturer : capturers) {
        if (capturer != nullptr) {
            capturer->stop();
            capturer->wait_for_capture_done();
            delete capturer;
        }
    }
}

void MultiCapturer::start_capturing(bool start_capture_thread) {
    // Set e_timestamp for each capturer
    int64_t e_timestamp = -1;
    int64_t f_timestamp = -1;
    for (auto& capturer : capturers) {
        if (capturer != nullptr) {
            capturer->init();
            int64_t capturer_e_timestamp = capturer->get_end_timestamp_usec();
            if (e_timestamp == -1 || capturer_e_timestamp < e_timestamp) {
                e_timestamp = capturer_e_timestamp;
            }
            int64_t capturer_f_timestamp = capturer->get_first_frame_timestamp_usec();
            if( f_timestamp == -1 || capturer_f_timestamp > f_timestamp) {
                f_timestamp = capturer_f_timestamp;
            }
        }
    }
    for (auto& capturer : capturers) {
        if (capturer != nullptr && capturer->is_initialized()) {
            capturer->set_end_timestamp_corrected_usec(e_timestamp);
            if(f_timestamp != -1) {
                unsigned int n_frames_to_drop = (f_timestamp - capturer->get_first_frame_timestamp_usec()) / (66*1000);
                capturer->fastforward_x_frames(n_frames_to_drop);
            }
            if(start_capture_thread) {
                capturer->create_capture_worker();
            }
        }
    }
}

Frame* MultiCapturer::poll_next_frame_for_capturer(unsigned int capturer_index) {
    if (capturer_index < capturers.size()) {
        return capturers[capturer_index]->poll_next_frame();
    }
    return nullptr;
}

void* MultiCapturer::get_calibration_for_capturer(unsigned int capturer_index) {
    if (capturer_index < capturers.size()) {
        return capturers[capturer_index]->get_calibration();
    }
    return nullptr;
}

uint32_t MultiCapturer::get_calibration_size_for_capturer(unsigned int capturer_index) {
    if (capturer_index < capturers.size()) {
        return capturers[capturer_index]->get_calibration_size();
    }
    return 0;
}

void MultiCapturer::set_cleanup_settings_for_capturer(unsigned int capturerer_index, FrameCleanupSettings _cleanup_settings) {
    if (capturerer_index < capturers.size()) {
        capturers[capturerer_index]->set_cleanup_settings(_cleanup_settings);
    }
}

PointCloud* MultiCapturer::poll_next_point_cloud_for_capturer(unsigned int capturer_index) {
    if (capturer_index < capturers.size()) {
        return capturers[capturer_index]->poll_next_point_cloud();
    }
    return nullptr;
}
RawFrame *MultiCapturer::poll_next_raw_frame_for_capturer(unsigned int capturer_index)
{
    if (capturer_index < capturers.size()) {
        return capturers[capturer_index]->poll_next_raw_frame();
    }
    return nullptr;
}

bool MultiCapturer::register_frame_ready_callback_for_capturer(unsigned int capturer_index, FrameReadyCallback cb) {
    if (capturer_index < capturers.size()) {
        capturers[capturer_index]->register_frame_ready_callback(cb);
        return true;
    }
    return false;
}

PointCloud *MultiCapturer::get_single_combined_point_cloud()
{
    std::vector<PointCloud*> point_clouds;
    point_clouds.reserve(capturers.size());
    unsigned int total_points = 0;
    for (auto& capturer : capturers) {
        Frame* frame = capturer->get_single_frame();
        if(frame == nullptr) {
            continue;
        }
        PointCloud* pc = frame->get_point_cloud();
        // If nullptr -> keep polling
        if (pc != nullptr) {
            //Log::log("Captured point cloud from capturer " + std::to_string(pc->n_points), LogColor::Green);
            total_points += pc->n_points;
            point_clouds.push_back(pc);
        }
    }
    return combine_point_clouds(total_points, point_clouds);
}

PointCloud *MultiCapturer::poll_next_combined_point_cloud()
{
    std::vector<PointCloud*> point_clouds;
    point_clouds.reserve(capturers.size());
    unsigned int total_points = 0;

    for (auto& capturer : capturers) {
        PointCloud* pc = capturer->poll_next_point_cloud();
        // If nullptr -> keep polling
        if (pc != nullptr) {
            //Log::log("Captured point cloud from capturer " + std::to_string(pc->n_points), LogColor::Green);
            total_points += pc->n_points;
            point_clouds.push_back(pc);
        }
    }
    return combine_point_clouds(total_points, point_clouds);
}

PointCloud *MultiCapturer::combine_point_clouds(unsigned int total_points, const std::vector<PointCloud *> &point_clouds)
{
    // If all nullptr -> return
    // Else calculate highest timestamp
    // Poll other cameras until they get good frame with timestamp close to highest timestamp
    // Set #frames to drop (without sleep) based on lowest timestamp
    // If frameNr == 0 calculate s_seek
    unsigned int frame_nr = current_frame_nr;
    current_frame_nr++;
    if(total_points == 0) {
        for(auto& pc : point_clouds) {
            if(pc != nullptr) {
                delete pc; // Free the individual point cloud
            }
        }
        return nullptr; // No point clouds captured
    }
    
    PointCloud* combined_pc = new PointCloud();
    combined_pc->n_points = total_points;
    combined_pc->capturer_id = 0; // Set to 0 or any other identifier if needed
    combined_pc->frame_nr = frame_nr;
    combined_pc->timestamp = point_clouds.empty() ? 0 : point_clouds[0]->timestamp;
    combined_pc->coords = new Vertex[combined_pc->n_points];
    combined_pc->colors = new Color[combined_pc->n_points];
    combined_pc->frame_pointer = nullptr; // Set to nullptr, as we don't want to free the frame pointer
    combined_pc->delete_arrays = true; // Set to true, so we can free the arrays in the destructor
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
