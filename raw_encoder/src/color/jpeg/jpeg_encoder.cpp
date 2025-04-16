#include "color/jpeg/jpeg_encoder.hpp"

// Constructor
JpegEncoder::JpegEncoder(unsigned int width, unsigned int height, JpegSettings* settings) 
    : ColorEncoder(width, height), jpeg_quality(settings->quality) {
    compressor = tjInitCompress();
}

// Destructor
JpegEncoder::~JpegEncoder() {
    if (compressor != nullptr) {
        tjDestroy(compressor);
    }
}

// compress_frame function
EncodedColor* JpegEncoder::compress_frame(uint8_t* raw_color) {
    std::unique_lock lk(m_enqueue);
    unsigned long jpeg_size = 0;
    unsigned char* compressed_image = nullptr;
    tjCompress2(compressor, static_cast<unsigned char*>(raw_color), width, 0, height, TJPF_RGB, &compressed_image, &jpeg_size, TJSAMP_444, jpeg_quality, TJFLAG_FASTDCT);
    return new EncodedJpeg(compressed_image, jpeg_size);
}