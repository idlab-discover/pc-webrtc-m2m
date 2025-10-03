#pragma once
#include "framework.h"
#include "capturer.hpp"
#include <k4a/k4a.h>
#include <k4arecord/playback.h>
#include <optional>
#include <chrono>
#pragma pack(push, 1)
struct PrerecordedKinectCaptureSettings {
    bool align_to_depth;
    float min_height;
    float max_height;
    float radius;
    float trafo[4][4];
    char cam_file[256];
};
#pragma pack(pop)

class PrerecordedKinectCapturer : public Capturer {
    public:
        PrerecordedKinectCapturer(
            unsigned int capture_id,
            unsigned int fps, FrameMode mode,
            FrameCleanupSettings cleanup_settings,
            PrerecordedKinectCaptureSettings* capture_settings
        ) try : 
            cam_file(std::string(capture_settings->cam_file)),
            min_height(capture_settings->min_height), 
            max_height(capture_settings->max_height),
            radius(capture_settings->radius),
            align_to_depth(capture_settings->align_to_depth), 
            Capturer(capture_id, mode, fps, cleanup_settings)
        {
            for(int i = 0; i < 4; i++) {
                for(int j = 0; j < 4; j++) {
                    trafo[i][j] = capture_settings->trafo[i][j];
                }
            }
            interframe_delay = std::chrono::milliseconds(1000 / fps);
            previous_time = std::chrono::high_resolution_clock::now();
        } catch(...) {
            
        };
        ~PrerecordedKinectCapturer() {
            if(camera_handle != nullptr) {
                k4a_playback_close(camera_handle);
            }
            if(transformation_handle != nullptr) {
                k4a_transformation_destroy(transformation_handle);
            }
           
            //pipe.stop();
           // frame_buffer.stop_buffer();
        }
        CAPTURER_SETUP_CODE init();
        CAPTURER_SETUP_CODE capture_next_frame();
        Frame* poll_next_frame();
        void fastforward_x_frames(unsigned int x);
        
        void* get_depth_intrinsics();
        void* get_color_intrinsics();
     
        void* get_calibration();
        uint32_t get_calibration_size() {
            return sizeof(KinectCalibration);
        }

        static void free_calibration(void* cal) {
            free_calibration_internal<KinectCalibration>(cal);
        };
         Frame* get_single_frame();
    private:
        int64_t prev_timestamp_usec = -1;
        std::string cam_file;
        float trafo[4][4];
        k4a_image_t xy_table = NULL;
        unsigned int depth_width;
        unsigned int depth_height;
        unsigned int color_width;
        unsigned int color_height;
        
        float min_height;
        float max_height;
        float radius;
        bool align_to_depth;

        k4a_playback_t camera_handle;
        k4a_record_configuration_t record_config;
        k4a_transformation_t transformation_handle;
        k4a_calibration_t cal;

        std::chrono::milliseconds interframe_delay;
        std::chrono::steady_clock::time_point previous_time;

        kinect_cam_cal create_camera_calibration(bool is_depth);
        void fill_extrensics(KinectCalibration& kin_cal);
        kinect_cam_ex copy_extrensics(k4a_calibration_extrinsics_t ex);
        kinect_cam_in copy_intrinsics(k4a_calibration_intrinsics_t in);

        void wait_for_next_frame();
        CAPTURER_SETUP_CODE capture_next_frame_internal();
       
};