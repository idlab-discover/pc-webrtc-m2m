#pragma once
#include "color/encoded_color.hpp"
#include <webp/encode.h>
class EncodedWebP : public EncodedColor {
    public:
        EncodedWebP();
        ~EncodedWebP();

        WebPMemoryWriter& get_writer();
        void init_buffer();
    private:
        WebPMemoryWriter writer;
};