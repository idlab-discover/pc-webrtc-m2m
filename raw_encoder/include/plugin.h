#pragma once
#ifdef WIN32
#define DLLExport __declspec(dllexport)
#else
#define DLLExport
#endif

// All exported functions should be declared here
extern "C"
{
	DLLExport int initialize(unsigned int width, unsigned int height, unsigned int jpeg_quality);
	DLLExport void set_logging(char* log_directory, int _log_level);
	DLLExport void clean_up();
	DLLExport uint32_t encode_frame(RawFrame* f);
	DLLExport DecodedDepth* decode_depth(unsigned char* data, unsigned int width, unsigned int height);
	DLLExport DecodedJpeg* decode_color(unsigned char* data, unsigned long size, unsigned int width, unsigned int height);
	DLLExport unsigned short* get_decoded_depth_data(DecodedDepth* ptr);
	DLLExport unsigned char* get_decoded_color_data(DecodedJpeg* ptr);
	DLLExport void free_decoded_depth(DecodedDepth* ptr);
	DLLExport void free_decoded_color(DecodedJpeg* ptr);
}
