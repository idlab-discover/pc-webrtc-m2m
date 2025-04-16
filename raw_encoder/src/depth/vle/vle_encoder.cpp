#include "depth/vle/vle_encoder.hpp"

VLEEncoder::VLEEncoder(unsigned int width, unsigned int height) 
    : nibblesWritten(0), word(0), DepthEncoder(width, height) 
{
}


VLEEncoder::~VLEEncoder() {
  
}
EncodedDepth* VLEEncoder::encode_depth(uint16_t* raw_depth) {

  
    std::unique_lock lk(m_enqueue);
    unsigned char* buffer = get_buffer(width, height);
    unsigned int size = compress_rvl(reinterpret_cast<unsigned short*>(raw_depth), buffer, width*height);
    return new EncodedDepth(size, buffer, this);
}

void VLEEncoder::encode_vle(int value, int*& p_buffer)
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

int VLEEncoder::compress_rvl(unsigned short *input, unsigned char *output, int numPixels)
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
