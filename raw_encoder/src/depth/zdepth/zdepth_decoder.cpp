#include "depth/zdepth/zdepth_decoder.hpp"

ZDepthDecoder::ZDepthDecoder() {

}
ZDepthDecoder::~ZDepthDecoder() {

}
DecodedDepth* ZDepthDecoder::decode_depth(unsigned char* encoded_depth, unsigned int width, unsigned int height) {
    std::unique_lock lk(m_enqueue);
    uint16_t* buffer = get_buffer(width, height);
    int _width, _height;
    decompressor.Decompress(reinterpret_cast<uint8_t*>(encoded_depth), _width, _height, buffer);
    return new DecodedDepth(width, height, buffer, this);
}