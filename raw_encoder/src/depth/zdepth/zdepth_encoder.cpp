#include "depth/zdepth/zdepth_encoder.hpp"
#include "depth/zdepth/encoded_zdepth.hpp"
ZDepthEncoder::ZDepthEncoder(unsigned int width, unsigned int height) 
    : DepthEncoder(width, height) {
}
ZDepthEncoder::~ZDepthEncoder() {
   
}
EncodedDepth* ZDepthEncoder::encode_depth(uint16_t* raw_depth) {
    std::unique_lock lk(m_enqueue);
    std::vector<uint8_t> compressed;
    compressor.Compress(width, height, raw_depth, compressed, true);
    return new EncodedZDepth(std::move(compressed), this);
}