#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "raw_frame.hpp"
#include "decoded_depth.hpp"

class DepthDecoder {
    public:
        DepthDecoder();
        ~DepthDecoder();
        DecodedDepth* decode_depth(unsigned char* raw_depth, unsigned int width, unsigned int height);
        void return_buffer(unsigned short* buffer);
    private:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;
        int nibblesWritten;
        int word;
        std::stack<unsigned short*> buffers;
        
        void decompress_rvl(unsigned char *input, unsigned short *output, int numPixels);
        int decode_vle(int*& p_buffer);
};  

// Jobs vs threads
