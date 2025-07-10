#pragma once
#include "point_cloud.hpp"
#include "raw_frame.hpp"
#include "raw_converter.hpp"
#include "capturer.hpp"
#include "multi_capturer/multi_capturer.hpp"
#ifdef WIN32
#define DLLExport __declspec(dllexport)
#else
#define DLLExport
#endif

// All exported functions should be declared here
extern "C"
{
	DLLExport void set_logging(char* log_directory, int _log_level);
	
	
	
	// Single Capturer functions
	//	    General functions
	DLLExport Capturer* create_new_capturer(uint32_t fps, 
		FrameMode mode, FrameCleanupSettings cleanup_settings, 
		CAPTURE_TYPE type, void* capture_settings);
	DLLExport void start_capturing(Capturer* capturer);
	DLLExport void set_capturer_frame_cleanup_settings(Capturer* capturer, 
		FrameCleanupSettings cleanup_settings);
	DLLExport void* get_calibration(Capturer* capturer);
	DLLExport uint32_t get_calibration_size(Capturer* capturer);
	//		Poll functions
	DLLExport PointCloud* poll_next_point_cloud(Capturer* capturer);
	DLLExport Frame* poll_next_frame(Capturer* capturer);
	DLLExport RawFrame* poll_next_raw_frame(Capturer* capturer);

	// Multi Capturer functions
	//		General functions
	DLLExport MultiCapturer* create_new_multi_capturer(uint32_t fps, 
		FrameMode mode, FrameCleanupSettings cleanup_settings, 
		CAPTURE_TYPE type, unsigned int n_settings, void** capture_settings);
	DLLExport void start_capturing_multi(MultiCapturer* capturer);
	DLLExport void set_cleanup_settings_for_capturer(MultiCapturer* capturer, unsigned int capturerer_index, FrameCleanupSettings _cleanup_settings);
	DLLExport void* get_calibration_for_capturer(MultiCapturer* capturer, unsigned int capturer_index);
	DLLExport uint32_t get_calibration_size_for_capturer(MultiCapturer* capturer, unsigned int capturer_index);
	DLLExport bool register_frame_ready_callback_for_capturer(MultiCapturer* capturer, unsigned int capturer_index, FrameReadyCallback cb);

	//		Poll functions
	DLLExport PointCloud* poll_next_combined_point_cloud(MultiCapturer* capturer);
	DLLExport Frame* poll_next_frame_for_capturer(MultiCapturer* capturer, unsigned int capturer_index);
	DLLExport PointCloud* poll_next_point_cloud_for_capturer(MultiCapturer* capturer, unsigned int capturer_index);
	DLLExport RawFrame* poll_next_raw_frame_for_capturer(MultiCapturer* capturer, unsigned int capturer_index);
	


	// Point cloud functions
	DLLExport size_t get_point_cloud_size(PointCloud* pc);

	// Frame functions
	DLLExport size_t get_frame_size(Frame* pc);
	DLLExport uint16_t* get_raw_depth(Frame* pc);
	DLLExport uint8_t* get_raw_color(Frame* pc);
	DLLExport PointCloud* convert_frame_to_pc(Frame* frame);
	DLLExport RawFrame* convert_frame_to_raw_frame(Frame* frame);

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
	DLLExport void free_multi_capturer(MultiCapturer* capturer);
	
	DLLExport void clean_up();
}