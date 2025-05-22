#pragma once
#include <vector>
#include <stack>
#include <mutex>
#include <zdepth/zdepth.hpp>
#include "raw_frame.hpp"
#include "depth/depth_encoder.hpp"
class ZDepthEncoder : public DepthEncoder {
    public:
        ZDepthEncoder(unsigned int width, unsigned int height);
        ~ZDepthEncoder();
        EncodedDepth* encode_depth(uint16_t* raw_depth);
       
    private:
        zdepth::DepthCompressor compressor;
};  

// Jobs vs threads
