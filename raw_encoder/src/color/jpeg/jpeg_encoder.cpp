#include "color/jpeg/jpeg_encoder.hpp"
#include "log.h"
#include "logging/logging_macros.hpp"
// Constructor
JpegEncoder::JpegEncoder(unsigned int width, unsigned int height, JpegSettings* settings) 
    : ColorEncoder(width, height), jpeg_quality(settings->quality) {
    LOG_STATUS_TO_FILE(NAME, Log::Status::Creating);

    compressor = tjInitCompress();

    LOG_STATUS_TO_FILE(NAME, Log::Status::Created);
}

// Destructor
JpegEncoder::~JpegEncoder() {
    LOG_STATUS_TO_FILE(NAME, Log::Status::Destroying);

    if (compressor != nullptr) {
        tjDestroy(compressor);
    }

    LOG_STATUS_TO_FILE(NAME, Log::Status::Destroyed);
}

// compress_frame function
EncodedColor* JpegEncoder::compress_frame(uint8_t* raw_color) {
    std::unique_lock lk(m_enqueue);
    unsigned long jpeg_size = 0;
    unsigned char* compressed_image = nullptr;
    tjCompress2(compressor, static_cast<unsigned char*>(raw_color), width, 0, height, TJPF_RGB, &compressed_image, &jpeg_size, TJSAMP_444, jpeg_quality, TJFLAG_FASTDCT);
    return new EncodedJpeg(compressed_image, jpeg_size);
}