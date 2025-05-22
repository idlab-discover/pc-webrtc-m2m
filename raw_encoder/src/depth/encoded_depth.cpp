#include "depth/encoded_depth.hpp"
#include "depth/depth_encoder.hpp"
EncodedDepth::~EncodedDepth() {
    if(buffer != nullptr) {
        encoder_ptr->return_buffer(buffer);
    }
    
}