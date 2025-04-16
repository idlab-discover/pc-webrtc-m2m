#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "raw_frame.hpp"
#include "depth/encoded_depth.hpp"
class DepthEncoder {
    public:
        DepthEncoder(unsigned int width, unsigned int height);
        virtual ~DepthEncoder();
        virtual EncodedDepth* encode_depth(uint16_t* raw_depth) = 0;
        void return_buffer(unsigned char* buffer);
    protected:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;
        std::stack<unsigned char*> buffers;
        unsigned char* get_buffer(unsigned int width, unsigned int height) {
            if(buffers.empty()) {
                buffers.push(new unsigned char[width*height*2]);
            }
            unsigned char* buffer = buffers.top();
            buffers.pop();
            return buffer;
        }
     
};  

// Jobs vs threads
