#pragma once
#include <vector>
#include <turbojpeg.h>
#include "raw_frame.hpp"
#include <mutex>
#include "color/jpeg/encoded_jpeg.hpp"
#include "color/color_encoder.hpp"

struct JpegSettings {
    unsigned int quality;
};

class JpegEncoder : public ColorEncoder {
    public:
        JpegEncoder(unsigned int width, unsigned int height, JpegSettings* settings);
        ~JpegEncoder();
        virtual EncodedColor* compress_frame(uint8_t* raw_color);
    private:
        unsigned int jpeg_quality;
        tjhandle compressor = nullptr;
        std::mutex m_enqueue;
};

// Jobs vs threads
