#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "raw_frame.hpp"
#include "depth/decoded_depth.hpp"

class DepthDecoder {
    public:
        DepthDecoder();
        virtual ~DepthDecoder();
        virtual DecodedDepth* decode_depth(unsigned char* raw_depth, unsigned int width, unsigned int height) = 0;
        void return_buffer(unsigned short* buffer);
    protected:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;
        std::stack<unsigned short*> buffers;

        unsigned short* get_buffer(unsigned int width, unsigned int height) {
            if(buffers.empty()) {
                buffers.push(new unsigned short[width*height]);
            }
            unsigned short* buffer = buffers.top();
            buffers.pop();
            return buffer;
        }
};  

// Jobs vs threads
