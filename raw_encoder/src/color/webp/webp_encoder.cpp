#include "color/webp/webp_encoder.hpp"
#include "color/webp/encoded_webp.hpp"
#include "log.h"
#include "logging/logging_macros.hpp"
// Constructor
WebPEncoder::WebPEncoder(unsigned int width, unsigned int height, WebPSettings* settings) 
    : ColorEncoder(width, height), quality(settings->quality), method(settings->method) {
    LOG_STATUS_TO_FILE(NAME, Log::Status::Creating);
    WebPConfigInit(&config);
    config.quality = quality;
    config.method = method;
    WebPPictureInit(&picture);
    picture.width = width;
    picture.height = height;

    LOG_STATUS_TO_FILE(NAME, Log::Status::Created);
}

// Destructor
WebPEncoder::~WebPEncoder() {
    LOG_STATUS_TO_FILE(NAME, Log::Status::Destroying);

    WebPPictureFree(&picture);

    LOG_STATUS_TO_FILE(NAME, Log::Status::Destroyed);
}

// compress_frame function
EncodedColor* WebPEncoder::compress_frame(uint8_t* raw_color) {
    std::unique_lock lk(m_enqueue);
    EncodedWebP* encoded_webp = new EncodedWebP();
    WebPMemoryWriter& writer = encoded_webp->get_writer();
    WebPPictureImportRGB(&picture, reinterpret_cast<unsigned char*>(raw_color), picture.width * 3);
    picture.writer = WebPMemoryWrite;
    picture.custom_ptr = &writer;
    WebPEncode(&config, &picture);
    encoded_webp->init_buffer();
    return encoded_webp;
}