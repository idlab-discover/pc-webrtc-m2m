#include "pch.h"
#include "log.h"

#include <stdio.h>
#include <string>
#include <stdio.h>
#include <sstream>
unsigned int Log::every_n_frames = 100;
bool Log::limit_logging = false;

void Log::log(const char* message, LogColor color) {
	if (callbackInstance != nullptr) {
		callbackInstance(message, (int)color, (int)strlen(message));
	}
}

void Log::log(const std::string& message, LogColor color) {
	const char* tmsg = message.c_str();
	if (callbackInstance != nullptr) {
		callbackInstance(tmsg, (int)color, (int)strlen(tmsg));
	}
}

void Log::log(const int message, LogColor color) {
	std::stringstream ss;
	ss << message;
	send_log(ss, color);
}

void Log::log(const char message, LogColor color) {
	std::stringstream ss;
	ss << message;
	send_log(ss, color);
}

void Log::log(const float message, LogColor color) {
	std::stringstream ss;
	ss << message;
	send_log(ss, color);
}

void Log::log(const double message, LogColor color) {
	std::stringstream ss;
	ss << message;
	send_log(ss, color);
}

void Log::log(const bool message, LogColor color) {
	std::stringstream ss;
	if (message) {
		ss << "true";
	}
	else {
		ss << "false";
	}
	send_log(ss, color);
}

void Log::log_to_file(const std::string &message, bool write_to_websocket)
{
	if(logToFileCallbackInstance != nullptr) {
		logToFileCallbackInstance(message.c_str(), message.length(), write_to_websocket);
	}
}

void Log::send_log(const std::stringstream& ss, const LogColor& color) {
	const std::string tmp = ss.str();
	const char* tmsg = tmp.c_str();
	if (callbackInstance != nullptr) {
		callbackInstance(tmsg, (int)color, (int)strlen(tmsg));
	}
}

long long Log::get_time() {
	// Get the current time in milliseconds
	auto now = std::chrono::system_clock::now();
	auto duration = now.time_since_epoch();
	auto millis = std::chrono::duration_cast<std::chrono::milliseconds>(duration).count();
	return millis;
}

// Create a callback delegate
void RegisterDebugCallback(FuncCallBack cb) {
	callbackInstance = cb;
}

void RegisterLogToFileCallback(LogToFileCallback cb)
{
    logToFileCallbackInstance = cb;
}

void set_logging_settings(bool limit_logging, unsigned int every_n_frames) {
	Log::limit_logging = limit_logging;
	Log::every_n_frames = every_n_frames;
}
