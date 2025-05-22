#include "depth/depth_codec_factory.hpp"
#include "depth/vle/vle_encoder.hpp"
#include "depth/vle/vle_decoder.hpp"
#include "depth/zdepth/zdepth_encoder.hpp"
#include "depth/zdepth/zdepth_decoder.hpp"
#include <iostream>

DepthCodecFactory &DepthCodecFactory::get_instance() {
    static DepthCodecFactory instance;
    return instance;
}

DepthCodecFactory::~DepthCodecFactory() {
}

DepthEncoder *DepthCodecFactory::create_depth_encoder(DepthCodecType codec, unsigned int width, unsigned int height, void* codec_settings)
{
    switch (codec)
    {
        case VLE: {
            return new VLEEncoder(width, height);
            break;
        }
        case ZDEPTH: {
            return new ZDepthEncoder(width, height);
            break;
        }
    }
    return nullptr;
}

DepthDecoder *DepthCodecFactory::create_depth_decoder(DepthCodecType codec)
{
    switch (codec)
    {
        case VLE: {
            return new VLEDecoder();
            break;
        }
        case ZDEPTH: {
            return new ZDepthDecoder();
            break;
        }
 
    }
    return nullptr;
}
