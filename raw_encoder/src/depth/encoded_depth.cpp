#include "depth/encoded_depth.hpp"
#include "depth/depth_encoder.hpp"
EncodedDepth::~EncodedDepth() {
    encoder_ptr->return_buffer(buffer);
}