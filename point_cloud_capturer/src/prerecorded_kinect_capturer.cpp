#include "prerecorded_kinect_capturer.hpp"
#include "kinect_frame.hpp"
#include "log.h"
#include "kinect/kinect_helper.hpp"
CAPTURER_SETUP_CODE PrerecordedKinectCapturer::init()
{
    k4a_result_t result = k4a_playback_open(cam_file.c_str(), &camera_handle);
    if (result != K4A_RESULT_SUCCEEDED) {
        Log::custom_log("init: could not open playback file " + cam_file + "|| " + std::to_string(sizeof(PrerecordedKinectCaptureSettings)), Default, LogColor::Red);
        return CAPTURER_SETUP_CODE::CameraDisconnected;
    }
    k4a_playback_get_record_configuration(camera_handle, &record_config);
    Log::custom_log("init: starting playback with FPS: " + std::to_string(record_config.camera_fps), Default, LogColor::White);
    if (record_config.color_format != K4A_IMAGE_FORMAT_COLOR_BGRA32) {
        Log::custom_log("init: format is not bgra", Default, LogColor::Red);
        return CAPTURER_SETUP_CODE::InvalidValue;
    }

    k4a_playback_get_calibration(camera_handle, &cal);
    transformation_handle = k4a_transformation_create(&cal);
    depth_width = cal.depth_camera_calibration.resolution_width;
    depth_height = cal.depth_camera_calibration.resolution_height;
    color_width = cal.color_camera_calibration.resolution_width;
    color_height = cal.color_camera_calibration.resolution_height;
    Log::custom_log(std::format("init: depth reso: {}x{} color reso: {}x{}", depth_width, depth_height, color_width, color_height), Default, LogColor::White);
    KinectHelper::create_xy_table(cal, align_to_depth, xy_table);
    s_timestamp_offset_usec = 66 * 1000 * 10;
   /* if(capture_id == 0) {
       // s_timestamp_offset_usec = 66 * 1000 * 1;
    } else {
        s_timestamp_offset_usec += 66 * 1000 * 2;
    }*/
    e_timestamp_usec = k4a_playback_get_last_timestamp_usec(camera_handle);
    k4a_playback_seek_timestamp(camera_handle, s_timestamp_offset_usec, K4A_PLAYBACK_SEEK_BEGIN);

    k4a_capture_t capture_handle = nullptr;
    k4a_playback_get_next_capture(camera_handle, &capture_handle);
    k4a_image_t color_image = k4a_capture_get_color_image(capture_handle); 
    first_frame_timestamp_usec = k4a_image_get_device_timestamp_usec(color_image);
    Log::custom_log(std::format("init: Cam {} first frame timestamp: {}", capturer_id, first_frame_timestamp_usec), Default, LogColor::White);
    k4a_image_release(color_image);
    k4a_capture_release(capture_handle);
    initialized = true;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

CAPTURER_SETUP_CODE PrerecordedKinectCapturer::capture_next_frame()
{
    try {
        wait_for_next_frame();
        CAPTURER_SETUP_CODE c = capture_next_frame_internal();
		if(c != CAPTURER_SETUP_CODE::StartedCorrectly) {
            return c;
        }
        
    } catch (...) {
        return CAPTURER_SETUP_CODE::CameraDisconnected;
    }
	frame_nr++;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

void PrerecordedKinectCapturer::wait_for_next_frame()
{
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
}

void PrerecordedKinectCapturer::fastforward_x_frames(unsigned int x)
{
    Log::custom_log(std::format("fastforward_x_frames: Cam {} fast forwarding {} frames", capturer_id, std::to_string(x)), Default, LogColor::Yellow);
    s_timestamp_offset_usec += x * 66 * 1000; // 66 ms per frame
    for (unsigned int i = 0; i < x; i++) {
        k4a_capture_t capture_handle = nullptr;
        k4a_stream_result_t result = k4a_playback_get_next_capture(camera_handle, &capture_handle);
        if (result == K4A_STREAM_RESULT_EOF) {
            Log::custom_log("fastforward_x_frames: end of playback reached at frame_nr" + std::to_string(frame_nr), Default, LogColor::Red);
            k4a_playback_seek_timestamp(camera_handle, s_timestamp_offset_usec, K4A_PLAYBACK_SEEK_BEGIN);
            result = k4a_playback_get_next_capture(camera_handle, &capture_handle);
        }
        if (result != K4A_STREAM_RESULT_SUCCEEDED) {
            Log::custom_log("fastforward_x_frames: could not capture next frame " + std::to_string(result), Default, LogColor::Red);
            return;
        }
        k4a_capture_release(capture_handle);
    }
}

CAPTURER_SETUP_CODE PrerecordedKinectCapturer::capture_next_frame_internal()
{
    bool found_valid_frame = false;
    auto frame_ready_callback_copy = frame_ready_callback_instance; // Prevents the use of a potentially invalidated callback during the loop
    while (!found_valid_frame && keep_working) {
        auto temp_frame = get_single_frame();
        if(temp_frame == nullptr) {
            return CAPTURER_SETUP_CODE::CameraDisconnected;
        }
        found_valid_frame = temp_frame->is_valid_frame();
        if(frame_ready_callback_copy != nullptr) {
            frame_ready_callback_copy(capturer_id, temp_frame, temp_frame->is_valid_frame());    
        } else {
            frame_buffer.add_to_buffer(temp_frame);
        }
    }
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}


void *PrerecordedKinectCapturer::get_calibration()
{
    KinectCalibration* kin_cal = new KinectCalibration{
        .depth_camera_calibration=create_camera_calibration(true),
        .color_camera_calibration=create_camera_calibration(false),
        .depth_mode=static_cast<unsigned int>(cal.depth_mode),
        .color_resolution=static_cast<unsigned int>(cal.color_resolution),
        .align_to_depth=align_to_depth,
    };
    fill_extrensics(*kin_cal);
    for(int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            kin_cal->trafo[i][j] = trafo[i][j];
        }
    }
    Log::custom_log(std::format("get_calibration: align: {}", kin_cal->align_to_depth), Default, LogColor::Orange);
    return reinterpret_cast<void*>(kin_cal);
}

 Frame *PrerecordedKinectCapturer::get_single_frame()
{
    bool found_valid_frame = false;

    while (!found_valid_frame) {
        k4a_capture_t capture_handle = nullptr;
        k4a_stream_result_t result = k4a_playback_get_next_capture(camera_handle, &capture_handle);
        
        if (result == K4A_STREAM_RESULT_EOF) {
            Log::custom_log("capture_next_frame: end of playback reached at frame_nr" + std::to_string(frame_nr), Default, LogColor::Red);
            k4a_playback_seek_timestamp(camera_handle, s_timestamp_offset_usec, K4A_PLAYBACK_SEEK_BEGIN);
            result = k4a_playback_get_next_capture(camera_handle, &capture_handle);
        }
        if (result != K4A_STREAM_RESULT_SUCCEEDED) {
            Log::custom_log("capture_next_frame: could not capture next frame " + std::to_string(result), Default, LogColor::Red);
            return nullptr;
        }
        KinectFrame* temp_frame = new KinectFrame(
            capturer_id,
            mode,
            capture_handle,
            transformation_handle,
            xy_table,
            trafo,
            depth_width,
            depth_height,
            color_width,
            color_height,
            align_to_depth,
            frame_nr,
            e_timestamp_corrected_usec,
            cleanup_settings
        );
        if(temp_frame->is_valid_frame() || !temp_frame->get_device_timestamp() > e_timestamp_corrected_usec) {
            prev_timestamp_usec = temp_frame->get_device_timestamp();
            return temp_frame;
        } else {
            k4a_playback_seek_timestamp(camera_handle, s_timestamp_offset_usec, K4A_PLAYBACK_SEEK_BEGIN);
            delete temp_frame;
        }
    }   
}

kinect_cam_cal PrerecordedKinectCapturer::create_camera_calibration(bool is_depth)
{
    k4a_calibration_camera_t cam_cal;
    if (is_depth) {
        cam_cal = cal.depth_camera_calibration;
    } else {
        cam_cal = cal.color_camera_calibration;
    }
    kinect_cam_cal kinect_cam_cal {
        .extrinsics=copy_extrensics(cam_cal.extrinsics),
        .intrinsics=copy_intrinsics(cam_cal.intrinsics),
        .resolution_width=cam_cal.resolution_width,
        .resolution_height=cam_cal.resolution_height,
        .metric_radius=cam_cal.metric_radius,
    };
    return kinect_cam_cal;
}

void PrerecordedKinectCapturer::fill_extrensics(KinectCalibration& kin_cal)
{
    for(int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            kin_cal.extrinsics[i][j] = copy_extrensics(cal.extrinsics[i][j]);
        }
    }
}

kinect_cam_ex PrerecordedKinectCapturer::copy_extrensics(k4a_calibration_extrinsics_t ex)
{
    kinect_cam_ex ex_out;
    for(int i = 0; i < 9; i++) {
        ex_out.rotation[i] = ex.rotation[i];
    }
    for(int i = 0; i < 3; i++) {
        ex_out.translation[i] = ex.translation[i];
    }
    return ex_out;
}

kinect_cam_in PrerecordedKinectCapturer::copy_intrinsics(k4a_calibration_intrinsics_t in)
{
    kinect_cam_in in_out;
    in_out.type = in.type;
    in_out.parameter_count = in.parameter_count;
    for(int i = 0; i < 15; i++) {
        in_out.parameters[i] = in.parameters.v[i];
    }
    return in_out;
}


Frame *PrerecordedKinectCapturer::poll_next_frame()
{
    return frame_buffer.poll_next_frame();
}

