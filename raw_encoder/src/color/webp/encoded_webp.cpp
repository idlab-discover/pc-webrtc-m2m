#include "color/webp/encoded_webp.hpp"
EncodedWebP::EncodedWebP() : EncodedColor(nullptr, 0) {
    WebPMemoryWriterInit(&writer);
}

EncodedWebP::~EncodedWebP() {
    if (compressed_image != nullptr) {
        WebPMemoryWriterClear(&writer);
    }
}

WebPMemoryWriter& EncodedWebP::get_writer() {
    return writer;
}

void EncodedWebP::init_buffer() {
    compressed_image = writer.mem;
    size = writer.size;
}