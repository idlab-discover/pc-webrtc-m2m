

#include "pch.h"
#include "framework.h"
#include "log.h"
#include "plugin.h"

#include <chrono>
#include <fstream>
#include <iostream>
#include <map>
#include <string>
#include <thread>
#include "rs2_frame.hpp"
#include "framebuffer.hpp"
#include "rs2_capturer.hpp"
#include "artificical_capturer.hpp"
#include "raw_frame.hpp"
#include "artificical_raw_converter.hpp"
#include "rs2_raw_converter.hpp"
#include "prerecorded_kinect_capturer.hpp"
#include "kinect_raw_converter.hpp"
#include "capturer_factory.hpp"
using namespace std;

uint32_t n_tiles;

static thread worker;
//static bool keep_working = true;
//static bool initialized = false;

//mutex m_receivers;


uint32_t frame_number;


/*
	This function allows to specify a directory in which logs are created, and allows to specify if a verbose mode
	should be used. It should be called once per session from within Unity.
*/
void set_logging(char* log_directory, int _log_level) {
	Log::set_logging(log_directory, _log_level);
}


/*
	This function is responsible for capturing incoming realsense data. It is called from within a thread, which is started by the
	initialize function. No action is required from within Unity.
*/
void start_capturing(Capturer* capturer, bool start_capture_thread) {
	Log::custom_log("start_capturing: Starting to capture frames from single camera", Verbose, LogColor::Yellow);
	capturer->start_capturing(start_capture_thread);
}

/*
	This function is responsible for initializing the DLL. It should be called once per session from within Unity,
	specifiying the required IP addresses and ports, the number of tiles that will be transmitted, and the client ID.
*/
Capturer* create_new_capturer(uint32_t fps, 
	FrameMode mode, FrameCleanupSettings cleanup_settings, 
	CAPTURE_TYPE type, void* capture_settings
) {
	return CapturerFactory::get_instance().create_capturer(0, fps, mode, cleanup_settings, type, capture_settings);
}


PointCloud* poll_next_point_cloud(Capturer* capturer) {
	return capturer->poll_next_point_cloud();
}

Frame* poll_next_frame(Capturer* capturer) {
	return capturer->poll_next_frame();
}

Frame* get_single_frame(Capturer* capturer) {
	return capturer->get_single_frame();
}

RawFrame* poll_next_raw_frame(Capturer* capturer) {
	return capturer->poll_next_raw_frame();
}

size_t get_point_cloud_size(PointCloud* frame) {
	if(frame == nullptr) return 0;
	return frame->n_points;
}
size_t get_frame_size(Frame* frame) {
	if (frame == nullptr) return 0;
	return frame->get_frame_size();
}

uint16_t* get_raw_depth(Frame* frame) {
	if (frame == nullptr) return 0;
	return frame->get_raw_depth();
}
uint8_t* get_raw_color(Frame* frame) {
	if (frame == nullptr) return 0;
	return frame->get_raw_colors();
}

// TODO check if this is optimized or if we should just pass it with the callback automatically
PointCloud* convert_frame_to_pc(Frame* frame) {
	if(frame == nullptr) {
		Log::custom_log("convert_frame_to_pc: Frame is null, cannot convert to point cloud", LOG_LEVEL::Default, LogColor::Red);
		return nullptr;
	}
	return frame->get_point_cloud();
}


RawFrame* convert_frame_to_raw_frame(Frame* frame) {
	if(frame == nullptr) {
		Log::custom_log("convert_frame_to_raw_frame: Frame is null, cannot convert to raw frame", LOG_LEVEL::Default, LogColor::Red);
		return nullptr;
	}
	return frame->get_raw_frame();
}

void free_point_cloud(PointCloud * frame) {
	if(frame == nullptr) return;
	delete frame;
}

void free_frame(Frame* frame) {
	if (frame == nullptr) return;
	delete frame;
}

void free_raw_frame(RawFrame* frame) {
	if (frame == nullptr) return;
	delete frame;
}

void* get_calibration(Capturer* capturer) {
	if(capturer == nullptr) return nullptr;
	return capturer->get_calibration();
}

uint32_t get_calibration_size(Capturer* capturer) {
	if(capturer == nullptr) return 0;
	return capturer->get_calibration_size();
}

RawConverter* create_new_raw_converter(CAPTURE_TYPE type, void* cal) {
	switch(type) {
		case CAPTURE_TYPE::Artifical: {
			Log::custom_log("create_new_raw_converter: Creating artificial raw converter", LOG_LEVEL::Default, LogColor::Orange);
			return new ArtificalRawConverter(cal);
		}
		case CAPTURE_TYPE::RealSense: {
			Log::custom_log("create_new_raw_converter: Creating realsense2 raw converter", LOG_LEVEL::Default, LogColor::Orange);
			return new RS2RawConverter(cal);
		}
		case CAPTURE_TYPE::PrerecordedKinect: {
			Log::custom_log("create_new_raw_converter: Creating prerecorded kinect raw converter", LOG_LEVEL::Default, LogColor::Orange);
			return new KinectRawConverter(cal);
		}
		default: {
			Log::custom_log("create_new_raw_converter: Invalid capture type", LOG_LEVEL::Default, LogColor::Red);
			return nullptr;
		}
	}
}

void convert_raw_frame(RawConverter* c, uint16_t* depth, uint8_t* color, Vector3* pos_out, Color32* col_out) {
	c->convert_raw(depth, color, pos_out, col_out);
}

void free_raw_converter(RawConverter* c) {
	if(c != nullptr) {
		delete c;
	}
}

void set_capturer_frame_cleanup_settings(Capturer* capturer, FrameCleanupSettings cleanup_settings) {
	if(capturer) {
		capturer->set_cleanup_settings(cleanup_settings);
	}
}

void free_capturer(Capturer* capturer) {
	if(capturer != nullptr) {
		capturer->stop();
		capturer->wait_for_capture_done();
		delete capturer;
	}
}

void free_capturer_calibration(CAPTURE_TYPE type, void* cal) {
	switch(type) {
		case CAPTURE_TYPE::Artifical: {
			ArtificalCapturer::free_calibration(cal);
			break;
		}
		case CAPTURE_TYPE::RealSense: {
			RS2Capturer::free_calibration(cal);
			break;
		}
		case CAPTURE_TYPE::PrerecordedKinect: {
			PrerecordedKinectCapturer::free_calibration(cal);
			break;
		}
		default: {
			Log::custom_log("free_capturer_calibration: Invalid capture type", LOG_LEVEL::Default, LogColor::Red);
			break;
		}
	}
}

MultiCapturer* create_new_multi_capturer(uint32_t fps, 
	FrameMode mode, FrameCleanupSettings cleanup_settings, 
	CAPTURE_TYPE type, unsigned int n_settings, void** capture_settings) 
{
	return CapturerFactory::get_instance().create_multi_capturer(fps, mode, cleanup_settings, type, n_settings, capture_settings);
}

void start_capturing_multi(MultiCapturer* capturer, bool start_capture_thread) {
	if(capturer == nullptr) {
		return;
	}
	Log::custom_log("start_capturing_multi: Starting to capture frames from multi camera", Verbose, LogColor::Yellow);
	capturer->start_capturing(start_capture_thread);
}


void* get_calibration_for_capturer(MultiCapturer* capturer, unsigned int capturer_index) {
	if(capturer == nullptr) {
		return nullptr;
	}
	return capturer->get_calibration_for_capturer(capturer_index);
}

uint32_t get_calibration_size_for_capturer(MultiCapturer* capturer, unsigned int capturer_index) {
	if(capturer == nullptr) {
		return 0;
	}
	return capturer->get_calibration_size_for_capturer(capturer_index);
}

void set_cleanup_settings_for_capturer(MultiCapturer* capturer, unsigned int capturerer_index, FrameCleanupSettings _cleanup_settings) {
	if(capturer == nullptr) {
		return;
	}
	capturer->set_cleanup_settings_for_capturer(capturerer_index, _cleanup_settings);
}

bool register_frame_ready_callback_for_capturer(MultiCapturer* capturer, unsigned int capturer_index, FrameReadyCallback cb) {
	if(capturer == nullptr) {
		return false;
	}
	return capturer->register_frame_ready_callback_for_capturer(capturer_index, cb);
}

PointCloud* poll_next_point_cloud_for_capturer(MultiCapturer* capturer, unsigned int capturer_index) {
	if(capturer == nullptr) {
		return nullptr;
	}
	return capturer->poll_next_point_cloud_for_capturer(capturer_index);
}

PointCloud* get_single_combined_point_cloud(MultiCapturer* capturer) {
	if(capturer == nullptr) {
		return nullptr;
	}
	return capturer->get_single_combined_point_cloud();
}

PointCloud* poll_next_combined_point_cloud(MultiCapturer* capturer) {
	if(capturer == nullptr) {
		return nullptr;
	}
	return capturer->poll_next_combined_point_cloud();
}

Frame* poll_next_frame_for_capturer(MultiCapturer* capturer, unsigned int capturer_index) {
	if(capturer == nullptr) {
		return nullptr;
	}
	return capturer->poll_next_frame_for_capturer(capturer_index);
}

RawFrame* poll_next_raw_frame_for_capturer(MultiCapturer* capturer, unsigned int capturer_index) {
	if(capturer == nullptr) {
		return nullptr;
	}
	return capturer->poll_next_raw_frame_for_capturer(capturer_index);
}


void free_multi_capturer(MultiCapturer* capturer) {
	if (capturer != nullptr) {
		delete capturer;
	}
}









/*
	This function is used to clean up threading and reset the required variables. It is called once per session from
	within Unity.
*/
void clean_up() {
	Log::custom_log("clean_up: Attempting to clean up", Verbose, LogColor::Orange);
/*
	// Check if the DLL has already been initialized
	if (initialized) {

		// Close sockets, using the mutex for sending data
		//unique_lock<mutex> guard(m_send_data);
		
		//guard.unlock();

		// Join the listening thread
		if (worker.joinable())
			worker.join();
		// TODO Cleanup Realsense2
		// Reset the initialized flag
		initialized = false;
		custom_log("clean_up: Cleaned up", Verbose, LogColor::Orange);
	}
	else {
		// No action is required
		custom_log("clean_up: Already cleaned up", Verbose, LogColor::Orange);
	}*/
}
