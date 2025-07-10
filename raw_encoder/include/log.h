#pragma once

#include <stdio.h>
#include <string>
#include <stdio.h>
#include <sstream>
#include "framework.h"

extern "C"
{
	typedef void(*FuncCallBack)(const char* message, int color, int size);
	static FuncCallBack callbackInstance = nullptr;
	DLLExport void RegisterDebugCallback(FuncCallBack cb);

	typedef void(*LogToFileCallback)(const char* message, int size, bool write_to_websocket);
	static LogToFileCallback logToFileCallbackInstance = nullptr;
	DLLExport void RegisterLogToFileCallback(LogToFileCallback cb);

	DLLExport void set_logging_settings(bool limit_logging, unsigned int every_n_frames);
}

enum class LogColor { Red, Green, Blue, Black, White, Yellow, Orange };

class Log {

public:
	enum Status {
		// Object Status
        Creating = 0,
        Created = 1,
        Destroying = 2,
        Destroyed = 3,
        Disposing = 4,
        Disposed = 5,
        Failed = 6,

        // Encoding Status
        StartEncodingRaw = 100,
        StartEncodingColor = 101,
        EndEncodingColor = 102,
        StartEncodingDepth = 103,
        EndEncodingDepth = 104,
        StartEncodingPC = 105,
        EndEncodingPC = 106,
        StartEncodingAudio = 107,
        EndEncodingAudio = 108,

        // Decoding Status
        StartDecodingRaw = 200,
        StartDecodingColor = 201,
        EndDecodingColor = 202,
        StartDecodingDepth = 203,
        EndDecodingDepth = 204,
        StartDecodingPC = 205,
        EndDecodingPC = 206,
        StartDecodingAudio = 207,
        EndDecodingAudio = 208,

        // Encoding Queue Status
        Enqueuing = 300,
        Enqueued = 301,
        Dequeuing = 302,
        Dequeued = 303,
        Dropped = 304,
        DroppedBecausePrevious = 305,

        // General Frame Status
        FrameCreated = 400,
        FrameCompleted = 401,
        FrameRendered = 402,
        FrameDropped = 403,
        FrameDestroyed = 404,

	};
	static bool limit_logging;
	static unsigned int every_n_frames;
	static void log(const char* message, LogColor color = LogColor::Black);
	static void log(const std::string& message, LogColor color = LogColor::Black);
	static void log(const int message, LogColor color = LogColor::Black);
	static void log(const char message, LogColor color = LogColor::Black);
	static void log(const float message, LogColor color = LogColor::Black);
	static void log(const double message, LogColor color = LogColor::Black);
	static void log(const bool message, LogColor color = LogColor::Black);

	static void log_to_file(const std::string& message, bool write_to_websocket=false);
	static long long get_time();
	static bool is_logging_limited() {
		return limit_logging;
	}
	static unsigned int get_every_n_frames() {
		return every_n_frames;
	}
private:
	static void send_log(const std::stringstream& ss, const LogColor& color);
	
};