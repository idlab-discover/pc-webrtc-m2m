#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "color/decoded_color.hpp"
class ColorDecoder {
    public:
        ColorDecoder();
        virtual ~ColorDecoder();
        virtual DecodedColor* decompress_frame(unsigned char* encoded_color, unsigned long size, unsigned int width, unsigned int height) = 0;
        virtual void return_buffer(unsigned char* buffer);
    protected:
        std::mutex m_enqueue;
        unsigned char* get_buffer(unsigned int width, unsigned int height) {
            if(buffers.empty()) {
                buffers.push(new unsigned char[width*height*3]);
            }
            unsigned char* buffer = buffers.top();
            buffers.pop();
            return buffer;
        }
        std::stack<unsigned char*> buffers;
};