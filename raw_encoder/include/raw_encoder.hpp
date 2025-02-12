#pragma once
#include <vector>

#include "raw_frame.hpp"
#include "jpeg_encoder.hpp"
#include "depth_encoder.hpp"


enum ENCODING_STATUS {
    S_Succes = 0,
    S_Fail = 1,
};
class EncodedRaw {
    public:
        EncodedRaw(uint64_t timestamp, unsigned int frame_nr, unsigned int width, unsigned int height, unsigned int n_points, EncodedDepth* enc_depth, EncodedJpeg* enc_color) 
            : timestamp(timestamp), frame_nr(frame_nr), width(width), height(height), n_points(n_points), enc_depth(enc_depth), enc_color(enc_color) {}
        ~EncodedRaw() {
            if(enc_depth != nullptr) {
                delete enc_depth;
            }
            
            if(enc_color != nullptr) {
                delete enc_color;
            }
            
        }
        uint64_t get_timestamp() {return timestamp;}
        unsigned int get_frame_nr() {return frame_nr;}
        unsigned int get_width() {return width;} 
        unsigned int get_height() {return height;} 
        unsigned int get_n_points() {return n_points;}
        EncodedDepth* get_enc_depth() {return enc_depth;}
        EncodedJpeg* get_enc_color() {return enc_color;}
    private:
        uint64_t timestamp;
        unsigned int frame_nr;
        unsigned int width;
        unsigned int height;
        unsigned int n_points
        EncodedDepth* enc_depth;
        EncodedJpeg* enc_color;
};
class RawEncoder {
    public:
        RawEncoder(unsigned int width, unsigned int height, unsigned int jpeg_quality) : width(width), height(height), dep(DepthEncoder(width, height)), jpg(width, height, jpeg_quality) {

        };
        EncodedRaw* encode_raw(RawFrame* pc);
        unsigned int get_encoded_size_depth() { return encoded_depth_size; };
        unsigned int get_encoded_size_color() { return encoded_depth_size; };
        unsigned char* get_encoded_depth() { return encoded_depth.data();};
        unsigned char* get_encoded_color() { return encoded_depth.data();};
    private:
        std::vector<unsigned char> encoded_depth;
        std::vector<unsigned char> encoded_color;
        unsigned int encoded_depth_size;
        unsigned int encoded_color_size;
        unsigned int width;
        unsigned int height;
        
        ENCODING_STATUS status;
        DepthEncoder dep;
        JpegEncoder jpg;
};

// Jobs vs threads
