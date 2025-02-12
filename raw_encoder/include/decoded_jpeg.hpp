#pragma once
class JpegDecoder;
class DecodedJpeg {
    public:
        DecodedJpeg(unsigned int width, unsigned int height, unsigned char* buffer, JpegDecoder* decoder_ptr);
        ~DecodedJpeg();
        unsigned char* get_buffer() {return buffer;}
    private:
        unsigned int width;
        unsigned int height;
        unsigned char* buffer;
        JpegDecoder* decoder_ptr;

};