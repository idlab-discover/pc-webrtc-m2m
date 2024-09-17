#pragma once
#include "point_cloud.hpp"
#ifdef WIN32
#define DLLExport __declspec(dllexport)
#else
#define DLLExport
#endif

// All exported functions should be declared here
extern "C"
{
	DLLExport void set_logging(char* log_directory, int _log_level);
	DLLExport PointCloud* poll_next_point_cloud();
	DLLExport size_t get_point_cloud_size(PointCloud* pc);
	DLLExport void free_point_cloud(PointCloud * pc);
	DLLExport int initialize(uint32_t width, uint32_t height, uint32_t fps, float min_dist, float max_dist, bool _use_cam);
	DLLExport void clean_up();
}