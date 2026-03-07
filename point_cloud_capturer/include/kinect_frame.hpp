#pragma once
#ifdef USE_KINECT
#include "frame.hpp"

#include <k4a/k4a.h>
#include <k4arecord/playback.h>
#include "log.h"
class KinectFrame : public Frame {
    public:
        KinectFrame(unsigned int capturer_id, FrameMode mode, k4a_capture_t capture_handle, k4a_transformation_t& transform_handle, const k4a_image_t& xy_table,
            const float (&trafo)[4][4],
            unsigned int depth_width, unsigned int depth_height,
            unsigned int color_width, unsigned int color_height,
            bool align_to_depth,
            unsigned int frame_nr,
            int64_t e_timestamp_corrected_usec,
            FrameCleanupSettings cleanup_settings) 
            :   capture_handle(capture_handle),
                depth_width(depth_width), depth_height(depth_height),
                color_width(color_width), color_height(color_height), Frame(capturer_id, frame_nr) 
        {
            depth_image = k4a_capture_get_depth_image(capture_handle);
            color_image = k4a_capture_get_color_image(capture_handle); 
            device_timestamp = k4a_image_get_device_timestamp_usec(color_image);
            if(e_timestamp_corrected_usec > -1 && device_timestamp > e_timestamp_corrected_usec) {
                Log::custom_log(std::format("KinectFrame: Device timestamp {} is greater than max timestamp {}, skipping frame", device_timestamp, e_timestamp_corrected_usec), Default, LogColor::Red);
                return;
            }
            if(!align_to_depth) {
                k4a_image_t transformed_depth_image_handle;
                k4a_image_create(K4A_IMAGE_FORMAT_DEPTH16, color_width, color_height, color_width*2, &transformed_depth_image_handle);
                k4a_transformation_depth_image_to_color_camera(transform_handle, depth_image, transformed_depth_image_handle);
                k4a_image_release(depth_image);
                depth_image = transformed_depth_image_handle;
            }
            if(depth_image == nullptr || color_image == nullptr) {
                Log::custom_log("KinectFrame: Could not get depth or color image from capture", Default, LogColor::Red);
                return;
            }
            
            if(frame_nr % 100 == 0) {
                Log::custom_log(std::format("KinectFrame: Cam {} Frame {} timestamp {}", capturer_id, frame_nr, device_timestamp), Default, LogColor::Orange);
            } 
            switch (mode)
            {
                case FrameMode::RealData: {
                    make_pc(xy_table, trafo);
                    break;
                } 
                case FrameMode::RawData: {
                    make_raw_data_arrays(xy_table, trafo, cleanup_settings);
                    break;
                }
                case FrameMode::Both: {
                    make_pc(xy_table, trafo);
                    make_raw_data_arrays(xy_table, trafo, cleanup_settings);
                    break;
                }
                    
            }
            if(n_points == 0) {
                Log::custom_log("KinectFrame: No valid points found in frame", Default, LogColor::Red);
                return;
            }
            is_valid = true;
            
        };
        ~KinectFrame() {
            if(depth_image != nullptr) {
                k4a_image_release(depth_image);
            }
            if(color_image != nullptr) {
                k4a_image_release(color_image);
            }
            if(capture_handle != nullptr) {
                k4a_capture_release(capture_handle);
            }
           
            
        }
        unsigned int get_frame_size() { return n_points; }
        Vertex* get_vertex_array() { return vertices.data(); };
        Color* get_color_array() { return colors.data();};

        uint16_t* get_raw_depth() {return (uint16_t*)k4a_image_get_buffer(depth_image);};
        uint8_t* get_raw_colors() {return raw_color.data();};
        
        unsigned int get_capture_width() { return color_width;};
        unsigned int get_capture_height() { return color_height;};
        unsigned int get_raw_n_points() {return n_points;};
        
    private:
        k4a_image_t depth_image;
        k4a_image_t color_image;
        k4a_capture_t capture_handle;
        void make_pc(const k4a_image_t& xy_table, const float (&trafo)[4][4]);
        void make_raw_data_arrays(const k4a_image_t& xy_table, const float (&trafo)[4][4], FrameCleanupSettings cleanup_settings);
        void apply_depth_filter_to_raw(const k4a_image_t& xy_table, const float (&trafo)[4][4], FrameCleanupSettings cleanup_settings);
        inline Vertex get_transformed_vertex(const k4a_float2_t& xy_table_data, const float (&trafo)[4][4], uint16_t depth);
        inline bool is_vertex_filtered(const Vertex& vertex);
        std::vector<Color> colors;
        std::vector<Vertex> vertices;
  
        std::vector<uint16_t> raw_depth;
        std::vector<uint8_t> raw_color;
        unsigned int color_width;
        unsigned int color_height;
        unsigned int depth_width;
        unsigned int depth_height;
        unsigned int n_points = 0;
        
};
#endif