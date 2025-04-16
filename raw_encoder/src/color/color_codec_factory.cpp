#include "color/color_codec_factory.hpp"
#include "color/jpeg/jpeg_encoder.hpp"
#include "color/jpeg/jpeg_decoder.hpp"
#include "color/webp/webp_encoder.hpp"
#include "color/webp/webp_decoder.hpp"
#include <iostream>

ColorCodecFactory &ColorCodecFactory::get_instance() {
    static ColorCodecFactory instance;
    return instance;
}

ColorCodecFactory::~ColorCodecFactory() {
}

ColorEncoder *ColorCodecFactory::create_color_encoder(ColorCodecType codec, unsigned int width, unsigned int height, void* codec_settings)
{
    switch (codec)
    {
    case JPEG: {
            return new JpegEncoder(width, height,  static_cast<JpegSettings*>(codec_settings));
            break;
        }
    case WEBP: {
            return new WebPEncoder(width, height, static_cast<WebPSettings*>(codec_settings));
            break;
        }
    }
    return nullptr;
}

ColorDecoder *ColorCodecFactory::create_color_decoder(ColorCodecType codec)
{
    switch (codec)
    {
    case JPEG: {
            return new JpegDecoder();
            break;
        }
    case WEBP: {
            return new WebPDecoder();
            break; 
    }
    }
    return nullptr;
}
