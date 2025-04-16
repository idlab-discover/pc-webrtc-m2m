#pragma once
#include <vector>
#include "raw_frame.hpp"
#include <mutex>
#include "color/encoded_color.hpp"


class ColorEncoder {
    public:
        ColorEncoder(unsigned int width, unsigned int height) : width(width), height(height) {
          
        }
        virtual ~ColorEncoder() = default;
        virtual EncodedColor* compress_frame(uint8_t* raw_color) = 0;
       
    protected:
        std::mutex m_enqueue;
        unsigned int width;
        unsigned int height;
};

// Jobs vs threads
