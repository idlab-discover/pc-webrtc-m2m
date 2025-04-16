#pragma once
#include <vector>
#include <stack>
#include <turbojpeg.h>
#include "color/color_decoder.hpp"
#include "raw_frame.hpp"
#include "color/jpeg/decoded_jpeg.hpp"
#include <mutex>
class JpegDecoder : public ColorDecoder {
    public:
        JpegDecoder();
        virtual ~JpegDecoder();
        virtual DecodedColor* decompress_frame(unsigned char* encoded_jpeg, unsigned long size, unsigned int width, unsigned int height);
    private:
        tjhandle decompressor = nullptr;
       
};

// Jobs vs threads
