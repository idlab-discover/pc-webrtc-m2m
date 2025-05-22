#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include <zdepth/zdepth.hpp>
#include "depth/depth_decoder.hpp"

class ZDepthDecoder : public DepthDecoder {
    public:
        ZDepthDecoder();
        ~ZDepthDecoder();
        DecodedDepth* decode_depth(unsigned char* raw_depth, unsigned int width, unsigned int height);
    
    private:
        zdepth::DepthCompressor decompressor;
};  

// Jobs vs threads
