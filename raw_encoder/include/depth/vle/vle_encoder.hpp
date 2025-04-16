#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "raw_frame.hpp"
#include "depth/depth_encoder.hpp"
class VLEEncoder : public DepthEncoder {
    public:
        VLEEncoder(unsigned int width, unsigned int height);
        ~VLEEncoder();
        EncodedDepth* encode_depth(uint16_t* raw_depth);
       
    private:
        int nibblesWritten;
        int word;
        
        void encode_vle(int value, int*& p_buffer);
        int compress_rvl(unsigned short *input, unsigned char *output, int numPixels);
};  

// Jobs vs threads
