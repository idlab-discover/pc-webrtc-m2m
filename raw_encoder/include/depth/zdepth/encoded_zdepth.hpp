#pragma once
#include <vector>
#include <stack>
#include "depth/encoded_depth.hpp"
class EncodedZDepth : public EncodedDepth {
    public:
        EncodedZDepth(std::vector<uint8_t>&& compressed, DepthEncoder* encoder_ptr);
        ~EncodedZDepth();
        unsigned char* get_bytes() {return buffer;}
        unsigned long get_size() {return size;}
    private:
        unsigned int size;
        unsigned char* buffer;
        DepthEncoder* encoder_ptr;
};