#include "encoded_depth.hpp"
#include "depth_encoder.hpp"
EncodedDepth::~EncodedDepth() {
    encoder_ptr->return_buffer(buffer);
}