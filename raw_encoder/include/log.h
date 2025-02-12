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
}

enum class LogColor { Red, Green, Blue, Black, White, Yellow, Orange };

class Log {

public:
	static void log(const char* message, LogColor color = LogColor::Black);
	static void log(const std::string message, LogColor color = LogColor::Black);
	static void log(const int message, LogColor color = LogColor::Black);
	static void log(const char message, LogColor color = LogColor::Black);
	static void log(const float message, LogColor color = LogColor::Black);
	static void log(const double message, LogColor color = LogColor::Black);
	static void log(const bool message, LogColor color = LogColor::Black);

private:
	static void send_log(const std::stringstream& ss, const LogColor& color);
};