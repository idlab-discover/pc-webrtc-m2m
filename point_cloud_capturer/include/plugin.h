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
	
	
	
	// Capturer functions
	//	    General functions
	DLLExport Capturer* create_new_capturer(uint32_t fps, 
		FrameMode mode, FrameCleanupSettings cleanup_settings, 
		CAPTURE_TYPE type, void* capture_settings);
	DLLExport void start_capturing(Capturer* capturer);
	DLLExport void set_capturer_frame_cleanup_settings(Capturer* capturer, 
		FrameCleanupSettings cleanup_settings);
	DLLExport void* get_calibration(Capturer* capturer);
	//		Poll functions
	DLLExport PointCloud* poll_next_point_cloud(Capturer* capturer);
	DLLExport Frame* poll_next_frame(Capturer* capturer);
	DLLExport RawFrame* poll_next_raw_frame(Capturer* capturer);

	// Point cloud functions
	DLLExport size_t get_point_cloud_size(PointCloud* pc);

	// Frame functions
	DLLExport size_t get_frame_size(Frame* pc);
	DLLExport uint16_t* get_raw_depth(Frame* pc);
	DLLExport uint8_t* get_raw_color(Frame* pc);

	// Raw converter functions
	DLLExport RawConverter* create_new_raw_converter(CAPTURE_TYPE type, void* cal);
	DLLExport void convert_raw_frame(RawConverter* c, uint16_t* depth, uint8_t* color, Vector3* pos_out, Color32* col_out);
	
	// Free memory functions
	DLLExport void free_point_cloud(PointCloud * pc);
	DLLExport void free_frame(Frame * pc);
	DLLExport void free_raw_frame(RawFrame * pc);
	DLLExport void free_capturer(Capturer* capturer);
	DLLExport void free_raw_converter(RawConverter* c);
	DLLExport void free_capturer_calibration(CAPTURE_TYPE type, void* cal);
	
	DLLExport void clean_up();
}