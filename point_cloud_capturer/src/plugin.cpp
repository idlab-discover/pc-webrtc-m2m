

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
using namespace std;

uint32_t n_tiles;

static thread worker;
static bool keep_working = true;
static bool initialized = false;

//mutex m_receivers;


uint32_t frame_number;

static string log_file = "";
static int log_level = 0;
static bool use_cam = false;
mutex m_logging;
mutex m_capturing;
std::condition_variable cv_capture;
bool capture_done = false;
Capturer* capturer = nullptr;

// TODO make objects
// Realsense2 stuff

string api_version = "1.0";


enum LOG_LEVEL : int {
	Default = 0,
	Verbose = 1,
	Debug = 2
};


/*
	This function is used to get the current date/time in a predefined format, used by the custom_log function.
*/
inline string get_current_date_time(bool date_only) {
	time_t now = time(0);
	char buf[80];
	struct tm tstruct;
#if defined(_WIN64) || defined(_WIN32)
	localtime_s(&tstruct, &now);
#else
	localtime_r(&now, &tstruct);
#endif
	if (date_only) {
		strftime(buf, sizeof(buf), "%Y-%m-%d", &tstruct);
	}
	else {
		strftime(buf, sizeof(buf), "%Y-%m-%d %X", &tstruct);
	}
	return string(buf);
};

/*
	This function is used to pass log messages to the user. Verbose logging can be enabled, and different colors can be
	used to inidicate a specific function (e.g., sending or receiving data).
*/
void custom_log(string message, int _log_level = 0, LogColor color = LogColor::Black) {
	unique_lock<mutex> guard(m_logging);
	if (_log_level <= log_level) {
		Log::log(message, color);
	}
	if (log_file != "") {
		ofstream ofs(log_file.c_str(), ios_base::out | ios_base::app);
		ofs << get_current_date_time(false) << '\t' << message << '\n';
		ofs.close();
	}
	guard.unlock();
}

/*
	This function allows to specify a directory in which logs are created, and allows to specify if a verbose mode
	should be used. It should be called once per session from within Unity.
*/
void set_logging(char* log_directory, int _log_level) {
	log_file = string(log_directory) + "\\" + get_current_date_time(true) + ".txt";
	Log::log("set_logging: Log directory set to " + string(log_directory), LogColor::Orange);
	log_level = _log_level;
	Log::log("set_logging: Log level set to " + to_string(log_level), LogColor::Orange);
}


/*
	This function is responsible for capturing incoming realsense data. It is called from within a thread, which is started by the
	initialize function. No action is required from within Unity.
*/
void start_capturing() {
	custom_log("start_capturing: Starting to capture frames from realsense2 camera", Verbose, LogColor::Yellow);
	capture_done = false;
	keep_working = true;
	while (keep_working) {

		auto code = capturer->capture_next_frame();
		if (code != 0) {
			keep_working = false;
		}
		// auto vertices = points.get_vertices();
		// auto texture_coordinates = points.get_texture_coordinates();
		 // Fill in array with raw data
	}
	
	std::unique_lock lk(m_capturing);
	capture_done = true;
	lk.unlock();
	cv_capture.notify_all();
}

/*
	This function is responsible for initializing the DLL. It should be called once per session from within Unity,
	specifiying the required IP addresses and ports, the number of tiles that will be transmitted, and the client ID.
*/
int initialize(uint32_t width, uint32_t height, uint32_t fps, float min_dist, float max_dist, bool _use_cam) {
	use_cam = _use_cam;
	try {
		if(use_cam) {
			capturer = new RS2Capturer(width, height, fps, min_dist, max_dist);
		} else {
			capturer = new ArtificalCapturer(10, fps);
		}
	} catch (CAPTURER_SETUP_CODE e) {
		initialized = true;
		return e;
	}
	auto code = capturer->init();
	if(code == 0) {
		worker = thread(start_capturing);
	}
	initialized = true;
	return code;
}

PointCloud* poll_next_point_cloud() {
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

size_t get_point_cloud_size(PointCloud* frame) {
	if(frame == nullptr) return 0;
	return frame->n_points;
}

void free_point_cloud(PointCloud * frame) {
	if(frame == nullptr) return;
	delete frame;
}

/*
	This function is used to clean up threading and reset the required variables. It is called once per session from
	within Unity.
*/
void clean_up() {
	custom_log("clean_up: Attempting to clean up", Verbose, LogColor::Orange);

	// Check if the DLL has already been initialized
	if (initialized) {

		// Halt sending/receiving operations
		keep_working = false;
		
		// Close sockets, using the mutex for sending data
		//unique_lock<mutex> guard(m_send_data);
		
		//guard.unlock();

		// Join the listening thread
		if (worker.joinable())
			worker.join();
		// TODO Cleanup Realsense2
		if(capturer != nullptr) {
			capturer->stop();
			std::unique_lock lk(m_capturing);
			cv_capture.wait(lk, [] { return capture_done; });
			delete capturer;
			capturer = nullptr;
		}
		

		// Reset the initialized flag
		initialized = false;
		custom_log("clean_up: Cleaned up", Verbose, LogColor::Orange);
	}
	else {
		// No action is required
		custom_log("clean_up: Already cleaned up", Verbose, LogColor::Orange);
	}
}
