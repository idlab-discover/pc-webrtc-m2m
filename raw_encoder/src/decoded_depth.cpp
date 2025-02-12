#include "decoded_depth.hpp"
#include "depth_decoder.hpp"
DecodedDepth::DecodedDepth(unsigned int width, unsigned int height, unsigned short* buffer, DepthDecoder* decoder_ptr) 
    : width(width), height(height), buffer(buffer), decoder_ptr(decoder_ptr) {}

DecodedDepth::~DecodedDepth() {
    decoder_ptr->return_buffer(buffer);
}