#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "framebuffer.hpp"
#include "point_cloud.hpp"
#include "raw_frame.hpp"
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

class Capturer;
extern "C" {
    typedef void(*FrameReadyCallback)(unsigned int capturer_id, Frame* frame_ptr, bool is_frame_valid);

    DLLExport void register_frame_ready_callback(Capturer* cap, FrameReadyCallback cb);
}


class Capturer {
    public:
        Capturer(unsigned int capturer_id, FrameMode mode, unsigned int fps, FrameCleanupSettings cleanup_settings) 
        : capturer_id(capturer_id), mode(mode), fps(fps), cleanup_settings(cleanup_settings) {
        
        };
        virtual ~Capturer() {
          frame_buffer.clear_buffer();
          //  frame_buffer.stop_buffer();
        }
        virtual CAPTURER_SETUP_CODE init() = 0;
        virtual CAPTURER_SETUP_CODE capture_next_frame() = 0;
        virtual void stop() { frame_buffer.stop_buffer(); keep_working=false; };
        virtual Frame* poll_next_frame() = 0; 
        virtual void* get_calibration() = 0;
        virtual uint32_t get_calibration_size() = 0;
        virtual inline Frame* get_single_frame() = 0;  
        PointCloud* poll_next_point_cloud();
        RawFrame* poll_next_raw_frame();
        void set_cleanup_settings(FrameCleanupSettings _cleanup_settings) {cleanup_settings=_cleanup_settings;}
        void start_capturing(bool start_capture_thread);
        void wait_for_capture_done();
        unsigned int get_capture_id() const { return capturer_id; }
        int64_t get_start_timestamp_usec() const { return s_timestamp_offset_usec; }
        int64_t get_end_timestamp_usec() const { return e_timestamp_usec; }
        int64_t get_end_timestamp_corrected_usec() const { return e_timestamp_corrected_usec; }
        int64_t get_first_frame_timestamp_usec() const { return first_frame_timestamp_usec; }
        void set_end_timestamp_corrected_usec(int64_t _e_timestamp_corrected_usec) { e_timestamp_corrected_usec=_e_timestamp_corrected_usec; }
        bool is_initialized() const { return initialized; }
        void create_capture_worker();
        virtual void fastforward_x_frames(unsigned int x) {}; // only use at init to prevent race conditions
        // TODO reset functio to start playback at 0 / reset frame counter and clear buffer
        void register_frame_ready_callback(FrameReadyCallback cb) {
            frame_buffer.clear_buffer(); // Clear the buffer to prevent old frames from being processed
            frame_ready_callback_instance = cb;
        }
    protected:
        bool initialized = false;
        unsigned int capturer_id = 0;
        int64_t s_timestamp_offset_usec = 0;
        int64_t e_timestamp_usec = -1;
        int64_t e_timestamp_corrected_usec = -1;
        int64_t first_frame_timestamp_usec = -1;
        std::mutex m_capturing;
        std::condition_variable cv_capture;
        bool capture_done = true;
        bool keep_working = false;
        FrameMode mode;
        unsigned int frame_nr = 0;
        unsigned int fps;
        FrameBuffer frame_buffer;
        std::jthread worker;
        FrameCleanupSettings cleanup_settings {
            .blackout_block_size=1,
            .should_apply_depth_filter=false,
            .should_cleanup_depth = false,
            .should_blackout=false,
        };
        template <typename T>
        static void free_calibration_internal(void* cal) { delete static_cast<T*>(cal); };
        FrameReadyCallback frame_ready_callback_instance = nullptr; // TODO probably need to wrap this in mutex maybe
           
    private:
        void start_capturing_internal();
};