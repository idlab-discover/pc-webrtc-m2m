#include "encoded_jpeg.hpp"

EncodedJpeg::EncodedJpeg(unsigned char* compressed_image, unsigned long size) : compressed_image(compressed_image), size(size) {}

EncodedJpeg::~EncodedJpeg() {
    if(compressed_image != nullptr) {
        tjFree(compressed_image);
    }
}