#include "pch.h"
#include "log.h"

#include <stdio.h>
#include <string>
#include <stdio.h>
#include <sstream>




/*
	This function is used to get the current date/time in a predefined format, used by the custom_log function.
*/
inline std::string Log::get_current_date_time(bool date_only) {
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
	return std::string(buf);
};

/*
	This function is used to pass log messages to the user. Verbose logging can be enabled, and different colors can be
	used to inidicate a specific function (e.g., sending or receiving data).
*/
void Log::custom_log(std::string message, int _log_level, LogColor color) {
	std::unique_lock<std::mutex> guard(m_logging);
	if (_log_level <= log_level) {
		Log::log(message, color);
	}
	if (log_file != "") {
		std::ofstream ofs(log_file.c_str(), std::ios_base::out | std::ios_base::app);
		ofs << get_current_date_time(false) << '\t' << message << '\n';
		ofs.close();
	}
	guard.unlock();
}

/*
	This function allows to specify a directory in which logs are created, and allows to specify if a verbose mode
	should be used. It should be called once per session from within Unity.
*/
void Log::set_logging(char* log_directory, int _log_level) {
	log_file = std::string(log_directory) + "\\" + get_current_date_time(true) + ".txt";
	Log::log("set_logging: Log directory set to " + std::string(log_directory), LogColor::Orange);
	log_level = _log_level;
	Log::log("set_logging: Log level set to " + std::to_string(log_level), LogColor::Orange);
}


void Log::log(const char* message, LogColor color) {
	if (callbackInstance != nullptr) {
		callbackInstance(message, (int)color, (int)strlen(message));
	}
}

void Log::log(const std::string message, LogColor color) {
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

void Log::send_log(const std::stringstream& ss, const LogColor& color) {
	const std::string tmp = ss.str();
	const char* tmsg = tmp.c_str();
	if (callbackInstance != nullptr) {
		callbackInstance(tmsg, (int)color, (int)strlen(tmsg));
	}
}

// Create a callback delegate
void RegisterDebugCallback(FuncCallBack cb) {
	callbackInstance = cb;
}