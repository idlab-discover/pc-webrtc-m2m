#include "color/jpeg/encoded_jpeg.hpp"

EncodedJpeg::EncodedJpeg(unsigned char* compressed_image, unsigned long size) : EncodedColor(compressed_image, size) {}

EncodedJpeg::~EncodedJpeg() {
    if(compressed_image != nullptr) {
        tjFree(compressed_image);
    }
}