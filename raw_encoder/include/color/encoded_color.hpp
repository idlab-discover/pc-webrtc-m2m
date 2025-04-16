#pragma once

class EncodedColor {
    public:
        EncodedColor(unsigned char* compressed_image, unsigned long size) : compressed_image(compressed_image), size(size) {};
        virtual ~EncodedColor() = default;
        unsigned char* get_bytes() {return compressed_image;}
        unsigned long get_size() {return size;}
    protected:
        unsigned char* compressed_image = nullptr;
        unsigned long size = 0;
};