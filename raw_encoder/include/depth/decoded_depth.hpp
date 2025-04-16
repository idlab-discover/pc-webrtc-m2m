#pragma once
#include <vector>
#include <stack>
class DepthDecoder;
class DecodedDepth {
    public:
        DecodedDepth(unsigned int width, unsigned int height, unsigned short* buffer, DepthDecoder* decoder_ptr);
        ~DecodedDepth();
        unsigned short* get_buffer() {return buffer;}
    private:
        unsigned int width;
        unsigned int height;
        unsigned short* buffer;
        DepthDecoder* decoder_ptr;
};