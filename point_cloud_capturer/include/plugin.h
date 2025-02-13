#pragma once
#include "point_cloud.hpp"
#include "raw_frame.hpp"
#include "raw_converter.hpp"
#include "capturer.hpp"
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
	DLLExport Frame* poll_next_frame();
	DLLExport RawFrame* poll_next_raw_frame();
	DLLExport size_t get_point_cloud_size(PointCloud* pc);
	DLLExport size_t get_frame_size(Frame* pc);
	DLLExport uint16_t* get_raw_depth(Frame* pc);
	DLLExport uint8_t* get_raw_color(Frame* pc);
	DLLExport void free_point_cloud(PointCloud * pc);
	DLLExport void free_frame(Frame * pc);
	DLLExport void free_raw_frame(RawFrame * pc);
	DLLExport RawConverter* create_new_raw_converter(bool use_cam, CapturerIntrinsics depth_intrinsics, CapturerIntrinsics color_intrinsics) ;
	DLLExport void free_raw_converter(RawConverter* c);
	DLLExport void convert_raw_frame(RawConverter* c, uint16_t* depth, uint8_t* color, Vector3* pos_out, Color32* col_out);
	DLLExport CapturerIntrinsics get_depth_intrinsics();
	DLLExport CapturerIntrinsics get_color_intrinsics();
	DLLExport int initialize(uint32_t width, uint32_t height, uint32_t fps, float min_dist, float max_dist, bool _use_cam, FrameMode mode);
	DLLExport void clean_up();
}