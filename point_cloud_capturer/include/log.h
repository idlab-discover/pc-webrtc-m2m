#pragma once

#include <stdio.h>
#include <string>
#include <stdio.h>
#include <sstream>
#include <fstream>
#include <iostream>
#include <mutex>
#include "framework.h"
enum LOG_LEVEL : int {
	Default = 0,
	Verbose = 1,
	Debug = 2
};
extern "C"
{
	typedef void(*FuncCallBack)(const char* message, int color, int size);
	static FuncCallBack callbackInstance = nullptr;
	DLLExport void RegisterDebugCallback(FuncCallBack cb);
}

enum class LogColor { Red, Green, Blue, Black, White, Yellow, Orange };

static std::string log_file = "";
static int log_level = 0;
static std::mutex m_logging;

class Log {

public:
	static void set_logging(char* log_directory, int _log_level);
	static void custom_log(std::string message, int _log_level = 0, LogColor color = LogColor::Black);
	static inline std::string get_current_date_time(bool date_only);
	static void log(const char* message, LogColor color = LogColor::Black);
	static void log(const std::string message, LogColor color = LogColor::Black);
	static void log(const int message, LogColor color = LogColor::Black);
	static void log(const char message, LogColor color = LogColor::Black);
	static void log(const float message, LogColor color = LogColor::Black);
	static void log(const double message, LogColor color = LogColor::Black);
	static void log(const bool message, LogColor color = LogColor::Black);

private:

//mutex m_capturing;
//std::condition_variable cv_capture;
//bool capture_done = false;
//Capturer* capturer = nullptr;

// TODO make objects
// Realsense2 stuff

std::string api_version = "1.0";

	static void send_log(const std::stringstream& ss, const LogColor& color);
};