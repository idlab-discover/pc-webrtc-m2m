

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
void start_capturing(Capturer* capturer) {
	Log::custom_log("start_capturing: Starting to capture frames from realsense2 camera", Verbose, LogColor::Yellow);
	capturer->start_capturing();
	Log::custom_log("start_capturing: Stopped capturing frames from realsense2 camera", Verbose, LogColor::Yellow);
}

/*
	This function is responsible for initializing the DLL. It should be called once per session from within Unity,
	specifiying the required IP addresses and ports, the number of tiles that will be transmitted, and the client ID.
*/
Capturer* create_new_capturer(uint32_t fps, 
	FrameMode mode, FrameCleanupSettings cleanup_settings, 
	CAPTURE_TYPE type, void* capture_settings
) {

	Capturer* capturer = nullptr;
	try {
		switch(type) {
			case CAPTURE_TYPE::Artifical: {
				Log::custom_log("create_new_capturer: Creating artificial capturer", LOG_LEVEL::Default, LogColor::Orange);
				capturer = new ArtificalCapturer(fps, mode, cleanup_settings, static_cast<ArtificalCaptureSettings*>(capture_settings));
				break;
			}
			case CAPTURE_TYPE::RealSense: {
				Log::custom_log("create_new_capturer: Creating realsense2 capturer", LOG_LEVEL::Default, LogColor::Orange);
				capturer = new RS2Capturer(fps, mode, cleanup_settings, static_cast<RS2CaptureSettings*>(capture_settings));
				break;
			}
			case CAPTURE_TYPE::PrerecordedKinect: {
				Log::custom_log("create_new_capturer: Creating prerecorded kinect capturer", LOG_LEVEL::Default, LogColor::Orange);
				capturer = new PrerecordedKinectCapturer(fps, mode, cleanup_settings, static_cast<PrerecordedKinectCaptureSettings*>(capture_settings));
				break;
			}
			default: {
				Log::custom_log("create_new_capturer: Invalid capture type", LOG_LEVEL::Default, LogColor::Red);
				capturer =  nullptr;
			}
		}
	
	} catch (CAPTURER_SETUP_CODE e) {
		return nullptr;
	}
	return capturer;
}


PointCloud* poll_next_point_cloud(Capturer* capturer) {
	Frame* frame = capturer->poll_next_frame();
	if(frame == nullptr) {
		return nullptr;
	}
	return new PointCloud{
		frame->get_timestamp(),
		frame->get_frame_nr(),
		frame->get_frame_size(),
		frame->get_vertex_array(),
		frame->get_color_array(),
		frame
	};
}

Frame* poll_next_frame(Capturer* capturer) {
	Frame* frame = capturer->poll_next_frame();
	return frame;
}

RawFrame* poll_next_raw_frame(Capturer* capturer) {
	Frame* frame = capturer->poll_next_frame();
	return new RawFrame{
		frame->get_timestamp(),
		frame->get_frame_nr(),
		frame->get_capture_width(),
		frame->get_capture_height(),
		frame->get_raw_n_points(),
		frame->get_raw_depth(),
		frame->get_raw_colors(),
		frame
	};
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
