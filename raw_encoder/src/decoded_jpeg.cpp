#include "decoded_jpeg.hpp"
#include "jpeg_decoder.hpp"

DecodedJpeg::DecodedJpeg(unsigned int width, unsigned int height, unsigned char* buffer, JpegDecoder* decoder_ptr) : width(width), height(height), buffer(buffer), decoder_ptr(decoder_ptr) {};
DecodedJpeg::~DecodedJpeg() {
    decoder_ptr->return_buffer(buffer);
}