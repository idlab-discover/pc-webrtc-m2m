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

void MultiCapturer::start_capturing() {
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
        
            capturer->create_capture_worker();
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