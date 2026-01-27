#include "ply_capturer.hpp"
#include "ply_frame.hpp"
#include <thread>
#include <filesystem>
CAPTURER_SETUP_CODE PlyCapturer::init()
{
    if (!std::filesystem::exists(directory_path)) {
        return CAPTURER_SETUP_CODE::InvalidValue;
    }
    for (const auto& entry : std::filesystem::directory_iterator(directory_path)) {
        if (entry.is_regular_file() && entry.path().extension() == ".ply") {
            ply_files.push_back(entry.path().string());
        }
    }
    if(ply_files.empty()) {
        return CAPTURER_SETUP_CODE::InvalidValue;
    }
    std::sort(ply_files.begin(), ply_files.end());
    initialized = true;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

CAPTURER_SETUP_CODE PlyCapturer::capture_next_frame()
{
    // TODO sleep
    timeBeginPeriod(1);
    auto temp_frame = get_single_frame(); // Get frame first so sleep can be changed based on processing time
    auto current_time = std::chrono::high_resolution_clock::now(); // Get the end time of the loop
    auto elapsed_time = std::chrono::duration_cast<std::chrono::milliseconds>(current_time - previous_time); // Calculate the elapsed time in milliseconds
    
    if (elapsed_time < interframe_delay) // If the elapsed time is less than the desired frame time, sleep for the remaining time
    {
        std::this_thread::sleep_for(interframe_delay - elapsed_time);
    }
    previous_time = std::chrono::high_resolution_clock::now();
    // Need to call end here for optimisation
    timeEndPeriod(1);
    if(temp_frame == nullptr) {
        return CAPTURER_SETUP_CODE::BackendError;
    }
    if(frame_ready_callback_instance != nullptr) {
        frame_ready_callback_instance(capturer_id, temp_frame, true);
    } else {
        frame_buffer.add_to_buffer(temp_frame);
    }
    
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

Frame *PlyCapturer::poll_next_frame()
{
    return frame_buffer.poll_next_frame();
}
void* PlyCapturer::get_calibration() {
    return nullptr;
}

Frame *PlyCapturer::get_single_frame()
{
    PlyFrame* ret = new PlyFrame(
        capturer_id,
        mode,
        ply_files[current_file_index],
        frame_nr
    );
    current_file_index++;
    if(current_file_index >= ply_files.size()) {
        current_file_index = 0;
    }
    frame_nr++;
    return ret;
}
