#pragma once
#include <vector>
#include <turbojpeg.h>
#include "raw_frame.hpp"
#include <mutex>
class EncodedJpeg {
    public:
        EncodedJpeg(unsigned char* compressed_image, unsigned long size) : compressed_image(compressed_image), size(size) {}
        ~EncodedJpeg() {
            if(compressed_image != nullptr) {
                tjFree(compressed_image);
            }
        }
        unsigned char* get_bytes() {return compressed_image;}
        unsigned long get_size() {return size;}
    private:
        unsigned char* compressed_image;
        unsigned long size;
};
class JpegEncoder {
    public:
        JpegEncoder(unsigned int width, unsigned int height, unsigned int jpeg_quality) : width(width), height(height), jpeg_quality(jpeg_quality) {
            compressor = tjInitCompress();

        }
        ~JpegEncoder() {
            if(compressor != nullptr) {
                tjDestroy(compressor);
            }
        }
        EncodedJpeg* compress_frame(uint8_t* raw_color) {
            std::unique_lock lk(m_enqueue);
            unsigned long jpeg_size = 0;
            unsigned char* compressed_image = nullptr;
            tjCompress2(compressor, static_cast<unsigned char*>(raw_color), width, 0, height, TJPF_RGB, &compressed_image, &jpeg_size, TJSAMP_444, jpeg_quality, TJFLAG_FASTDCT);
            return new EncodedJpeg(compressed_image, jpeg_size);
        };
    private:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;
        unsigned int jpeg_quality;
        tjhandle compressor = nullptr;
};

// Jobs vs threads
