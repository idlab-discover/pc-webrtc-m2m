#include "multi_capturer/multi_capturer.hpp"

MultiCapturer::MultiCapturer(uint32_t fps, 
    FrameMode mode, FrameCleanupSettings cleanup_settings, 
    CAPTURE_TYPE type, unsigned int n_settings, void* capture_settings) {
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
    for (auto& capturer : capturers) {
        if (capturer != nullptr) {
            capturer->start_capturing();
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