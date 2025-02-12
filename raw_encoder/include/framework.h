#pragma once

#if defined(_WIN32) || defined(_WIN64)
// Exclude rarely-used stuff from Windows headers
#define WIN32_LEAN_AND_MEAN
// Include Windows header files
#include <windows.h>
#include <winsock2.h>
#include <Ws2tcpip.h>
// Winsock Library
#pragma comment(lib,"ws2_32.lib")

#define DLLExport __declspec(dllexport)
#else
#include <unistd.h>
#include <sys/socket.h>
#include <netinet/in.h>
#include <arpa/inet.h>
#include <errno.h>
#include <time.h>
#define DLLExport

#endif

#include <stdio.h>
#include <iostream>
#include <chrono>
#include <memory.h>
#include <map>
#include <vector>
#include <queue>
#include <thread>
#include <mutex>