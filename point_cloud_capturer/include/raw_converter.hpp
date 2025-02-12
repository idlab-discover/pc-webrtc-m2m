#pragma once
#include "framework.h"
struct Vector3 {
    float x;
    float y;
    float z;
};
struct Color32 {
    unsigned char r;
    unsigned char g;
    unsigned char b;
    unsigned char a;
};
class RawConverter {
    public:
        RawConverter(unsigned int width, unsigned int height) : width(width), height(height) {}
        virtual ~RawConverter() {};
        virtual void convert_raw(uint16_t* depth, uint8_t* color, Vector3* p_out, Color32* c_out) = 0;
        virtual void stop() {};
    protected:
        unsigned int width;
        unsigned int height;

};