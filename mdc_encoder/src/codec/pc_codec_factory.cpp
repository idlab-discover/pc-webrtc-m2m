#pragma once
/*#include <map>
#include <string>
#include "pc_encoder.hpp"
#include "pc_decoder.hpp"

enum PCCodecType {
    DRACO,
};


class PointCloudCodecFactory {
  public:
    static PointCloudCodecFactory &get_instance();

    PointCloudCodecFactory(PointCloudCodecFactory const &) = delete;
    void operator=(const PointCloudCodecFactory &) = delete;

    ~PointCloudCodecFactory();

    PointCloudEncoder* create_encoder(PCCodecType codec, void* codec_settings);
    PointCloudDecoder* create_decoder(PCCodecType codec);
private:
    PointCloudCodecFactory() = default;
};
*/