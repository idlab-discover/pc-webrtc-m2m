#include "artificical_capturer.hpp"
#include "artificical_frame.hpp"
#include <thread>

CAPTURER_SETUP_CODE ArtificalCapturer::init()
{
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

CAPTURER_SETUP_CODE ArtificalCapturer::capture_next_frame()
{
    // TODO sleep
    timeBeginPeriod(1);
    auto current_time = std::chrono::high_resolution_clock::now(); // Get the end time of the loop
    auto elapsed_time = std::chrono::duration_cast<std::chrono::milliseconds>(current_time - previous_time); // Calculate the elapsed time in milliseconds
    
    if (elapsed_time < interframe_delay) // If the elapsed time is less than the desired frame time, sleep for the remaining time
    {
        std::this_thread::sleep_for(interframe_delay - elapsed_time);
    }
    previous_time = std::chrono::high_resolution_clock::now();
    // Need to call end here for optimisation
    timeEndPeriod(1);
    frame_buffer.add_to_buffer(new ArtificalFrame(
        side_size,
        frame_nr
    ));
    frame_nr++;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

Frame *ArtificalCapturer::poll_next_frame()
{
    return frame_buffer.poll_next_frame();
}
