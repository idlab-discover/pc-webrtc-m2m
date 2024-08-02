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

enum class Color { Red, Green, Blue, Black, White, Yellow, Orange };

class Log {

public:
	static void log(const char* message, Color color = Color::Black);
	static void log(const std::string message, Color color = Color::Black);
	static void log(const int message, Color color = Color::Black);
	static void log(const char message, Color color = Color::Black);
	static void log(const float message, Color color = Color::Black);
	static void log(const double message, Color color = Color::Black);
	static void log(const bool message, Color color = Color::Black);

private:
	static void send_log(const std::stringstream& ss, const Color& color);
};
