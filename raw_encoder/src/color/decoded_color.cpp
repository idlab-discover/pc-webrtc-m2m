#include "color/decoded_color.hpp"
#include "color/color_decoder.hpp"
DecodedColor::DecodedColor(
    unsigned int width, unsigned int height, unsigned char* buffer, ColorDecoder* decoder_ptr) 
    :   width(width), height(height), 
        buffer(buffer), decoder_ptr(decoder_ptr) {};

DecodedColor::~DecodedColor() {
    decoder_ptr->return_buffer(buffer);
}