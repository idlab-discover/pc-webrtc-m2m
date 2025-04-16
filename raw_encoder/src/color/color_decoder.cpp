#include "color/color_decoder.hpp"
ColorDecoder::ColorDecoder() {
}
ColorDecoder::~ColorDecoder() {
    while(!buffers.empty()) {
        delete[] buffers.top();
        buffers.pop();
    }
}

void ColorDecoder::return_buffer(unsigned char* buffer) {
    std::unique_lock lk(m_enqueue);
    buffers.push(buffer);
}