#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "framebuffer.hpp"
#include "frame.hpp"
#include <mutex>
#include <thread>

enum CAPTURE_TYPE : int {
    Artifical = 0,
    RealSense = 1,
    PrerecordedRealSense = 2,
    Kinect = 3,
    PrerecordedKinect = 4,
};

enum CAPTURER_SETUP_CODE : int {
	StartedCorrectly = 0,
	CameraDisconnected = 1,
	BackendError = 2,
	InvalidValue = 3,
	WrongApiCallSeq = 4,
	NotImpl = 5,
	DeviceInRecovery = 6,
	UnknownException = 7
};

#pragma pack(push, 1)
struct ArtificialCalibration {
    unsigned int side_size;     
};
#pragma pack(pop)

#pragma pack(push, 1)
struct realsense_in {
    unsigned int  width;    
    unsigned int  height;  
    unsigned int  model;
    float         ppx;       
    float         ppy;     
    float         fx;     
    float         fy;        
    float         coeffs[5]; 
};

struct RealsenseCalibration {
    realsense_in depth_in;
    realsense_in color_in;
};
#pragma pack(pop)

#pragma pack(push, 1)
struct kinect_cam_ex
{
    float rotation[9];    /**< 3x3 Rotation matrix stored in row major order */
    float translation[3]; /**< Translation vector, x,y,z (in millimeters) */
};
struct kinect_cam_in
{
    unsigned int type;                 /**< Type of calibration model used*/
    unsigned int parameter_count;                      /**< Number of valid entries in parameters*/
    float parameters[15]; /**< Calibration parameters*/
};
struct kinect_cam_cal
{
    kinect_cam_ex extrinsics; /**< Extrinsic calibration data. */
    kinect_cam_in intrinsics; /**< Intrinsic calibration data. */
    int resolution_width;                    /**< Resolution width of the calibration sensor. */
    int resolution_height;                   /**< Resolution height of the calibration sensor. */
    float metric_radius;                     /**< Max FOV of the camera. */
};
struct KinectCalibration {
    kinect_cam_cal depth_camera_calibration;
    kinect_cam_cal color_camera_calibration;
    
    kinect_cam_ex extrinsics[4][4];

    unsigned int depth_mode;             /**< Depth camera mode for which calibration was obtained. */
    unsigned int color_resolution; /**< Color camera resolution for which calibration was obtained. */
    float trafo[4][4];
    bool align_to_depth;
};
#pragma pack(pop)



class Capturer {
    public:
        Capturer(FrameMode mode, unsigned int fps, FrameCleanupSettings cleanup_settings) : mode(mode), fps(fps), cleanup_settings(cleanup_settings) {
           
        };
        virtual ~Capturer() {
          frame_buffer.clear_buffer();
          //  frame_buffer.stop_buffer();
        }
        virtual CAPTURER_SETUP_CODE init() = 0;
        virtual CAPTURER_SETUP_CODE capture_next_frame() = 0;
        virtual void stop() { frame_buffer.stop_buffer(); keep_working=false; if(worker.joinable()) worker.join(); };
        virtual Frame* poll_next_frame() = 0; 
        virtual void* get_calibration() = 0;
        void set_cleanup_settings(FrameCleanupSettings _cleanup_settings) {cleanup_settings=_cleanup_settings;}
        void start_capturing();
        void wait_for_capture_done();

    protected:
        std::mutex m_capturing;
        std::condition_variable cv_capture;
        bool capture_done = false;
        bool keep_working = false;
        FrameMode mode;
        unsigned int frame_nr = 0;
        unsigned int fps;
        FrameBuffer frame_buffer;
        std::thread worker;
        FrameCleanupSettings cleanup_settings {
            .blackout_block_size=1,
            .should_apply_depth_filter=false,
            .should_cleanup_depth = false,
            .should_blackout=false,
        };
        template <typename T>
        static void free_calibration_internal(void* cal) { delete static_cast<T*>(cal); };
    private:
        void start_capturing_internal();
};