#pragma once
#ifdef WIN32
#define DLLExport __declspec(dllexport)
#else
#define DLLExport
#endif

// All exported functions should be declared here
extern "C"
{
	DLLExport int initialize();
	DLLExport void set_logging(char* log_directory, int _log_level);
	DLLExport void clean_up();
	DLLExport uint32_t encode_pc(PointCloud* pc);
	DLLExport uint32_t get_encoded_size(DracoMDCEncoder* enc);
	DLLExport char* get_raw_data(DracoMDCEncoder* enc);
	DLLExport DracoMDCDecoder* decode_pc(char* data, uint32_t size);
	DLLExport uint32_t get_n_points(DracoMDCDecoder* dec); 
	DLLExport float* get_point_array(DracoMDCDecoder* dec);
	DLLExport uint8_t* get_color_array(DracoMDCDecoder* dec);
	DLLExport void free_encoder(DracoMDCEncoder* enc);
	DLLExport void free_decoder(DracoMDCDecoder* dec);
	DLLExport void free_description(Description* dsc);

	
}
