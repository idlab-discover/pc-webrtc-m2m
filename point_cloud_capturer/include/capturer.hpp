#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "framebuffer.hpp"
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
struct CapturerIntrinsics {
    unsigned int  width;    
    unsigned int  height;  
    unsigned int  model;
    float         ppx;       
    float         ppy;     
    float         fx;     
    float         fy;        
    float         coeffs[5]; 
};

class Capturer {
    public:
        Capturer(FrameMode mode, unsigned int fps) : mode(mode), fps(fps) {

        };
        virtual ~Capturer() {
          frame_buffer.clear_buffer();
          //  frame_buffer.stop_buffer();
        }
        virtual CAPTURER_SETUP_CODE init() = 0;
        virtual CAPTURER_SETUP_CODE capture_next_frame() = 0;
        virtual void stop() { frame_buffer.stop_buffer(); };
        virtual Frame* poll_next_frame() = 0; 
        virtual CapturerIntrinsics get_depth_intrinsics() = 0;
        virtual CapturerIntrinsics get_color_intrinsics() = 0;
    protected:
        FrameMode mode;
        unsigned int frame_nr = 0;
        unsigned int fps;
        FrameBuffer frame_buffer;

};