#include "depth_encoder.hpp"

DepthEncoder::DepthEncoder(unsigned int width, unsigned int height) : width(width), height(height) {
           
}
DepthEncoder::~DepthEncoder() {
    while(!buffers.empty()) {
        delete[] buffers.top();
        buffers.pop();
    }
}
EncodedDepth* DepthEncoder::encode_depth(uint16_t* raw_depth) {
    std::unique_lock lk(m_enqueue);
    if(buffers.empty()) {
        buffers.push(new unsigned char[width*height*2]);
    }
    unsigned char* buffer = buffers.top();
    buffers.pop();
    unsigned int size = compress_rvl(reinterpret_cast<unsigned short*>(raw_depth), buffer, width*height);
    return new EncodedDepth(size, buffer, this);
}
void DepthEncoder::return_buffer(unsigned char* buffer) {
    std::unique_lock lk(m_enqueue);
    buffers.push(buffer);
}

void DepthEncoder::encode_vle(int value, int*& p_buffer)
{
    do
    {
        int nibble = value & 0x7; // lower 3 bits
        if (value >>= 3)
            nibble |= 0x8; // more to come
        word <<= 4;
        word |= nibble;
        if (++nibblesWritten == 8) // output word
        {
            *p_buffer++ = word;
            nibblesWritten = 0;
            word = 0;
        }
    } while (value);
}

int DepthEncoder::compress_rvl(unsigned short *input, unsigned char *output, int numPixels)
{
    int* buffer;
    int* p_buffer;
    buffer = p_buffer = (int *)output;
    nibblesWritten = 0;
    word = 0;
    unsigned short *end = input + numPixels;
    unsigned short previous = 0;
    while (input != end)
    {
        int zeros = 0, nonzeros = 0;
        for (; (input != end) && !*input; input++, zeros++)
            ;
        encode_vle(zeros, p_buffer); // number of zeros
        for (unsigned short *p = input; (p != end) && *p++; nonzeros++)
            ;
        encode_vle(nonzeros, p_buffer); // number of nonzeros
        for (int i = 0; i < nonzeros; i++)  
        {
            unsigned short current = *input++;
            int delta = current - previous;
            int positive = (delta << 1) ^ (delta >> 31);
            encode_vle(positive, p_buffer); // nonzero value
            previous = current;
        }
    }
    if (nibblesWritten) // last few values
        *p_buffer++ = word << 4 * (8 - nibblesWritten);
    return int((char *)p_buffer - (char *)buffer); // num bytes
}   