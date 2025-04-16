#include "depth/depth_encoder.hpp"

DepthEncoder::DepthEncoder(unsigned int width, unsigned int height) : width(width), height(height) {
           
}
DepthEncoder::~DepthEncoder() {
    while(!buffers.empty()) {
        delete[] buffers.top();
        buffers.pop();
    }
}

void DepthEncoder::return_buffer(unsigned char* buffer) {
    std::unique_lock lk(m_enqueue);
    buffers.push(buffer);
}