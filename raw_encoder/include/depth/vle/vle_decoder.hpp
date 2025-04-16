#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include "raw_frame.hpp"
#include "depth/depth_decoder.hpp"

class VLEDecoder : public DepthDecoder {
    public:
        VLEDecoder();
        ~VLEDecoder();
        DecodedDepth* decode_depth(unsigned char* raw_depth, unsigned int width, unsigned int height);
    
    private:
        int nibblesWritten;
        int word;
  
        void decompress_rvl(unsigned char *input, unsigned short *output, int numPixels);
        int decode_vle(int*& p_buffer);
};  

// Jobs vs threads
