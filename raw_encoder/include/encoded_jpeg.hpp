#pragma once
#include <turbojpeg.h>

class EncodedJpeg {
    public:
        EncodedJpeg(unsigned char* compressed_image, unsigned long size);
        ~EncodedJpeg();
        
        unsigned char* get_bytes() {return compressed_image;}
        unsigned long get_size() {return size;}
    private:
        unsigned char* compressed_image;
        unsigned long size;
};