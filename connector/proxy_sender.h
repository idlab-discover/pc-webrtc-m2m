#pragma once

#define DLLExport __declspec(dllexport)

// All exported functions should be declared here
extern "C"
{
	DLLExport void set_logfile(char* logfile_str);
	DLLExport void set_frame_data(void* d);
	DLLExport void set_control_data(void* d);
	DLLExport int setup_connection(char* server_str, uint32_t port);
	DLLExport void start_listening();
	DLLExport int next_frame();
	DLLExport int next_control_packet();
	DLLExport void clean_up();
	DLLExport int send_frame_data(void* data, uint32_t size);
	DLLExport int send_control_data(void* data, uint32_t size);
}
