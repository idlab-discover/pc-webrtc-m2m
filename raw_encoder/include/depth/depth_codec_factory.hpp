#pragma once
#include <map>
#include <string>
#include "depth/depth_encoder.hpp"
#include "depth/depth_decoder.hpp"

enum DepthCodecType {
    VLE,
    ZDEPTH,
};


class DepthCodecFactory {
  public:
    static DepthCodecFactory &get_instance();

    DepthCodecFactory(DepthCodecFactory const &) = delete;
    void operator=(const DepthCodecFactory &) = delete;

    ~DepthCodecFactory();

    DepthEncoder* create_depth_encoder(DepthCodecType codec, unsigned int width, unsigned int height, void* codec_settings);
    DepthDecoder* create_depth_decoder(DepthCodecType codec);
private:
    DepthCodecFactory() = default;
};
