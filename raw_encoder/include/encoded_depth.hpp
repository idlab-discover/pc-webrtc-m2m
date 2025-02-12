#pragma once
#include <vector>
#include <stack>
#include "raw_frame.hpp"
class DepthEncoder;
class EncodedDepth {
    public:
        EncodedDepth(unsigned int size, unsigned char* buffer, DepthEncoder* encoder_ptr) : size(size), buffer(buffer), encoder_ptr(encoder_ptr) {}
        ~EncodedDepth();
        unsigned char* get_bytes() {return buffer;}
        unsigned long get_size() {return size;}
    private:
        unsigned int size;
        unsigned char* buffer;
        DepthEncoder* encoder_ptr;
};