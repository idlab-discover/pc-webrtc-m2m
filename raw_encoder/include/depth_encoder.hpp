#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "raw_frame.hpp"
#include "encoded_depth.hpp"
class DepthEncoder {
    public:
        DepthEncoder(unsigned int width, unsigned int height);
        ~DepthEncoder();
        EncodedDepth* encode_depth(uint16_t* raw_depth);
        void return_buffer(unsigned char* buffer);
    private:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;

        int nibblesWritten;
        int word;
        std::stack<unsigned char*> buffers;
        
        void encode_vle(int value, int*& p_buffer);
        int compress_rvl(unsigned short *input, unsigned char *output, int numPixels);
};  

// Jobs vs threads
