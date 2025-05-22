#include "depth/zdepth/encoded_zdepth.hpp"

EncodedZDepth::EncodedZDepth(std::vector<uint8_t>&& compressed, DepthEncoder* encoder_ptr) 
    : EncodedDepth(static_cast<unsigned int>(compressed.size()), 
        static_cast<unsigned char*>(compressed.data()), 
        encoder_ptr) {
}

EncodedZDepth::~EncodedZDepth() {
    buffer = nullptr;   
}