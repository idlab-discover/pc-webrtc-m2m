#pragma once
#include <vector>
#include <stack>
#include <turbojpeg.h>
#include "raw_frame.hpp"
#include "decoded_jpeg.hpp"
#include <mutex>
class JpegDecoder {
    public:
        JpegDecoder();
        ~JpegDecoder();
        DecodedJpeg* decompress_frame(unsigned char* encoded_jpeg, unsigned long size, unsigned int width, unsigned int height);
        void return_buffer(unsigned char* buffer);
    private:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;
        unsigned int jpeg_quality;
        tjhandle decompressor = nullptr;
        std::stack<unsigned char*> buffers;
};

// Jobs vs threads
