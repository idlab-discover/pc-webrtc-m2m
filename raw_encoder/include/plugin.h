#pragma once
#include "color/color_codec_factory.hpp"
#include "depth/depth_codec_factory.hpp"
#ifdef WIN32
#define DLLExport __declspec(dllexport)
#else
#define DLLExport
#endif

// All exported functions should be declared here
extern "C"
{
	DLLExport int initialize(unsigned int width, unsigned int height, 
		ColorCodecType col_codec, void* col_codec_settings, 
		DepthCodecType dep_codec, void* dep_codec_settings
	);
	DLLExport void set_logging(char* log_directory, int _log_level);
	DLLExport void clean_up();
	DLLExport uint32_t encode_frame(RawFrame* f);
	DLLExport DecodedDepth* decode_depth(DepthDecoder* depth_dec, unsigned char* data, unsigned int width, unsigned int height);
	DLLExport DecodedColor* decode_color(ColorDecoder* color_dec, unsigned char* data, unsigned long size, unsigned int width, unsigned int height);
	DLLExport unsigned short* get_decoded_depth_data(DecodedDepth* ptr);
	DLLExport unsigned char* get_decoded_color_data(DecodedColor* ptr);
	DLLExport void free_decoded_depth(DecodedDepth* ptr);
	DLLExport void free_decoded_color(DecodedColor* ptr);
	DLLExport ColorDecoder* create_color_decoder(ColorCodecType codec);
	DLLExport DepthDecoder* create_depth_decoder(DepthCodecType codec);
	DLLExport void free_color_decoder(ColorDecoder* ptr);
	DLLExport void free_depth_decoder(DepthDecoder* ptr);
}
