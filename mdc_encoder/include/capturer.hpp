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


class Capturer {
    public:
        Capturer(unsigned int fps) : fps(fps) {

        };
        virtual ~Capturer() {
          frame_buffer.clear_buffer();
          //  frame_buffer.stop_buffer();
        }
        virtual CAPTURER_SETUP_CODE init() = 0;
        virtual CAPTURER_SETUP_CODE capture_next_frame() = 0;
        virtual void stop() { frame_buffer.stop_buffer(); };
        virtual Frame* poll_next_frame() = 0; 
    protected:
        unsigned int frame_nr = 0;
        unsigned int fps;
        FrameBuffer frame_buffer;

};