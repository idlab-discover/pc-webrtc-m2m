#pragma once
class ColorDecoder;
class DecodedColor {
    public:
        DecodedColor(unsigned int width, unsigned int height, unsigned char* buffer, ColorDecoder* decoder_ptr);
        ~DecodedColor();
        unsigned char* get_buffer() {return buffer;}
    private:
        unsigned int width;
        unsigned int height;
        unsigned char* buffer;
        ColorDecoder* decoder_ptr;

};