#include "artificical_capturer.hpp"
#include "artificical_frame.hpp"
#include <thread>

CAPTURER_SETUP_CODE ArtificalCapturer::init()
{
    initialized = true;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

CAPTURER_SETUP_CODE ArtificalCapturer::capture_next_frame()
{
    // TODO sleep
    #ifdef _WIN32
    timeBeginPeriod(1);
    #endif
    auto current_time = std::chrono::steady_clock::now(); // Get the end time of the loop
    auto elapsed_time = std::chrono::duration_cast<std::chrono::milliseconds>(current_time - previous_time); // Calculate the elapsed time in milliseconds
    
    if (elapsed_time < interframe_delay) // If the elapsed time is less than the desired frame time, sleep for the remaining time
    {
        std::this_thread::sleep_for(interframe_delay - elapsed_time);
    }
    previous_time = std::chrono::steady_clock::now();
    // Need to call end here for optimisation
    #ifdef _WIN32
    timeEndPeriod(1);
    #endif
    auto temp_frame = get_single_frame();
    if(frame_ready_callback_instance != nullptr) {
        frame_ready_callback_instance(capturer_id, temp_frame, true);
    } else {
        frame_buffer.add_to_buffer(temp_frame);
    }
    frame_nr++;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

Frame *ArtificalCapturer::poll_next_frame()
{
    return frame_buffer.poll_next_frame();
}
void* ArtificalCapturer::get_calibration() {
    return new ArtificialCalibration{side_size};
}

Frame *ArtificalCapturer::get_single_frame()
{
    return new ArtificalFrame(
        capturer_id,
        mode,
        side_size,
        frame_nr
    );
}
