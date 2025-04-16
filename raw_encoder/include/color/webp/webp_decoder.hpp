#pragma once
#include <vector>
#include <stack>
#include <webp/decode.h>
#include "color/color_decoder.hpp"
#include "raw_frame.hpp"
#include "color/webp/decoded_webp.hpp"
#include <mutex>

class WebPDecoder : public ColorDecoder {
    public:
        WebPDecoder();
        virtual ~WebPDecoder();
        virtual DecodedColor* decompress_frame(unsigned char* encoded_jpeg, unsigned long size, unsigned int width, unsigned int height);
    private:
        WebPDecoderConfig config;
       
};

// Jobs vs threads
