#pragma once
#include <vector>
#include <webp/encode.h>
#include "raw_frame.hpp"
#include <mutex>
#include "color/color_encoder.hpp"
#include "color/webp/encoded_webp.hpp"

struct WebPSettings {
    unsigned int quality;
    unsigned int method;
};

class WebPEncoder : public ColorEncoder {
    public:
        WebPEncoder(unsigned int width, unsigned int height, WebPSettings* settings);
        ~WebPEncoder();
        virtual EncodedColor* compress_frame(uint8_t* raw_color);
    private:
        unsigned int quality;
        unsigned int method;

        WebPConfig config;
        WebPPicture picture;
        std::mutex m_enqueue;
};

// Jobs vs threads
