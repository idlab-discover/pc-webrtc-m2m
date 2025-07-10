#pragma once
#include <vector>

#include "raw_frame.hpp"
#include "color/color_codec_factory.hpp"
#include "depth/depth_codec_factory.hpp"
#include "log.h"


enum ENCODING_STATUS {
    S_Succes = 0,
    S_Fail = 1,
};
class EncodedRaw {
    public:
        EncodedRaw(uint64_t timestamp, unsigned int capturer_id, unsigned int frame_nr, unsigned int width, unsigned int height, unsigned int n_points, EncodedDepth* enc_depth, EncodedColor* enc_color) 
            : timestamp(timestamp), capturer_id(capturer_id), frame_nr(frame_nr), width(width), height(height), n_points(n_points), enc_depth(enc_depth), enc_color(enc_color) {}
        ~EncodedRaw() {
            if(enc_depth != nullptr) {
                delete enc_depth;
            }
            
            if(enc_color != nullptr) {
                delete enc_color;
            }
            
        }
        uint64_t get_timestamp() {return timestamp;}
        unsigned int get_capturer_id() {return capturer_id;}
        unsigned int get_frame_nr() {return frame_nr;}
        unsigned int get_width() {return width;} 
        unsigned int get_height() {return height;} 
        unsigned int get_n_points() {return n_points;}
        EncodedDepth* get_enc_depth() {return enc_depth;}
        EncodedColor* get_enc_color() {return enc_color;}
    private:
        uint64_t timestamp;
        unsigned int capturer_id;
        unsigned int frame_nr;
        unsigned int width;
        unsigned int height;
        unsigned int n_points;
        EncodedDepth* enc_depth;
        EncodedColor* enc_color;
};
class RawEncoder {
    public:
        RawEncoder(unsigned int width, unsigned int height, 
            ColorCodecType col_type, void* col_codec_settings, 
            DepthCodecType dep_type, void* dep_codec_settings) : 
            width(width), height(height), 
            dep(DepthCodecFactory::get_instance().create_depth_encoder(dep_type, width, height, dep_codec_settings)), 
            col(ColorCodecFactory::get_instance().create_color_encoder(col_type, width, height, col_codec_settings))
        {
        };
        ~RawEncoder() {
            if(dep != nullptr) {
                delete dep;
            }
            if(col != nullptr) {
                delete col;
            }
        }
        EncodedRaw* encode_raw(RawFrame* pc);
        unsigned int get_encoded_size_depth() { return encoded_depth_size; };
        unsigned int get_encoded_size_color() { return encoded_depth_size; };
        unsigned char* get_encoded_depth() { return encoded_depth.data();};
        unsigned char* get_encoded_color() { return encoded_color.data();};

        bool is_ready() {return col != nullptr;}
    private:
        const std::string NAME = "RawEncoder";
        std::vector<unsigned char> encoded_depth;
        std::vector<unsigned char> encoded_color;
        unsigned int encoded_depth_size;
        unsigned int encoded_color_size;
        unsigned int width;
        unsigned int height;
        
        ENCODING_STATUS status;
        DepthEncoder* dep;
        ColorEncoder* col;
};

// Jobs vs threads
