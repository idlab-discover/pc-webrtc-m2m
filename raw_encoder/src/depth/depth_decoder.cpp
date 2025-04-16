#include "depth/depth_decoder.hpp"
DepthDecoder::DepthDecoder() : width(width), height(height) {
           
}

DepthDecoder::~DepthDecoder() {
    while(!buffers.empty()) {
        delete[] buffers.top();
        buffers.pop();
    }
}


void DepthDecoder::return_buffer(unsigned short* buffer) {
    std::unique_lock lk(m_enqueue);
    buffers.push(buffer);
}
