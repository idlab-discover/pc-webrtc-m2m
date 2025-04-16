#pragma once
#include <map>
#include <string>
#include "color/color_encoder.hpp"
#include "color/color_decoder.hpp"

enum ColorCodecType {
    JPEG,
    WEBP,
};


class ColorCodecFactory {
  public:
    static ColorCodecFactory &get_instance();

    ColorCodecFactory(ColorCodecFactory const &) = delete;
    void operator=(const ColorCodecFactory &) = delete;

    ~ColorCodecFactory();

    ColorEncoder* create_color_encoder(ColorCodecType codec, unsigned int width, unsigned int height, void* codec_settings);
    ColorDecoder* create_color_decoder(ColorCodecType codec);
private:
    ColorCodecFactory() = default;
};
