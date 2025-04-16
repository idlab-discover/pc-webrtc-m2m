#pragma once
#include <turbojpeg.h>
#include "color/encoded_color.hpp"
class EncodedJpeg : public EncodedColor{
    public:
        EncodedJpeg(unsigned char* compressed_image, unsigned long size);
        ~EncodedJpeg();
};