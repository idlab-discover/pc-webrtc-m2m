#include "draco_mdc_encoder.hpp"
#include "draco_mdc_decoder.hpp"
#include "encoding_queue.hpp"
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

using namespace std;

uint32_t n_tiles;

static thread worker;
static bool keep_working = true;
static bool initialized = false;

//mutex m_receivers;


uint32_t frame_number;

static string log_file = "";
static int log_level = 0;
mutex m_logging;
mutex m_capturing;
std::condition_variable cv_capture;
bool capture_done = false;
EncodingQueue* enc_queue;

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

int initialize() {
	custom_log("initialize: inting", Default, LogColor::Orange);
	enc_queue = new EncodingQueue(2);
	initialized = true;
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
		delete enc_queue;
		enc_queue = nullptr;

		// Reset the initialized flag
		initialized = false;
		custom_log("clean_up: Cleaned up", Verbose, LogColor::Orange);
	}
	else {
		// No action is required
		custom_log("clean_up: Already cleaned up", Verbose, LogColor::Orange);
	}
}

uint32_t encode_pc(PointCloud* pc) {
	// TODO
	//  Check number of active frames in queue
	//	If more than X = dont enter in queue and wait for place to become frame 
	//			* Set current_waiter PointCloud* to pc
	//			* When current_waiter is set = alert previous current_waiter that he can free himself
	//			* Use same condition variable?
	//					* notify_all probably
	//			* Set current_waiter = nullptr when going into real queue
	//  Subsample into three layers
	//			* Make job for each and insert into pool
	//  If job ready => callback to send to SFU
	//  All jobs ready => remove frame from queue and signal

	enc_queue->enqueue_pc(pc);
	return 0;
}
uint32_t get_encoded_size(DracoMDCEncoder* enc) {
	return enc != nullptr ? enc->get_encoded_size() : 0;
}
char* get_raw_data(DracoMDCEncoder* enc) {
	return enc != nullptr ? enc->get_raw_data() : nullptr;
}
DracoMDCDecoder* decode_pc(char* data, uint32_t size) {
	DracoMDCDecoder* dec = new DracoMDCDecoder();
	dec->decode_pc(data, size);
	return dec;
}
uint32_t get_n_points(DracoMDCDecoder* dec) {
	return dec != nullptr ? dec->get_n_points() : 0;
}

float* get_point_array(DracoMDCDecoder* dec) {
	return dec != nullptr ? dec->get_point_array() : nullptr;
}

uint8_t* get_color_array(DracoMDCDecoder* dec) {
	return dec != nullptr ? dec->get_color_array() : nullptr;
}

void free_encoder(DracoMDCEncoder* enc) {
	if(enc != nullptr) {
		delete enc;
	}
}
void free_decoder(DracoMDCDecoder* dec) {
	if(dec != nullptr) {
		delete dec;
	}
}

void free_description(Description* dsc) {
	if(dsc != nullptr) {
		delete dsc;
	}
}
