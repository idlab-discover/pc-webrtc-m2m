#include "depth/vle/vle_decoder.hpp"


VLEDecoder::VLEDecoder() : nibblesWritten(0), word(0), DepthDecoder() {}
VLEDecoder::~VLEDecoder() {
  
}

DecodedDepth* VLEDecoder::decode_depth(unsigned char* raw_depth, unsigned int width, unsigned int height) {
    std::unique_lock lk(m_enqueue);
    unsigned short* buffer = get_buffer(width, height);
    decompress_rvl(raw_depth, buffer, width*height);
    return new DecodedDepth(width, height, buffer, this);
}


void VLEDecoder::decompress_rvl(unsigned char *input, unsigned short *output, int numPixels)
{
    int* buffer;
    int* p_buffer;
    buffer = p_buffer = (int *)input;
    nibblesWritten = 0;
    word = 0;
    unsigned short current, previous = 0;
    int numPixelsToDecode = numPixels;
    while (numPixelsToDecode)
    {
        int zeros = decode_vle(p_buffer); // number of zeros
        numPixelsToDecode -= zeros;
        for (; zeros; zeros--)
            *output++ = 0;
        int nonzeros = decode_vle(p_buffer); // number of nonzeros
        numPixelsToDecode -= nonzeros;
        for (; nonzeros; nonzeros--)
        {
            int positive = decode_vle(p_buffer); // nonzero value
            int delta = (positive >> 1) ^ -(positive & 1);
            current = previous + delta;
            *output++ = current;
            previous = current;
        }
    }
}

int VLEDecoder::decode_vle(int*& p_buffer)
{
    unsigned int nibble;
    int value = 0, bits = 29;
    do
    {
        if (!nibblesWritten)
        {
            word = *p_buffer++; // load word
            nibblesWritten = 8;
        }
        nibble = word & 0xf0000000;
        value |= (nibble << 1) >> bits;
        word <<= 4;
        nibblesWritten--;
        bits -= 3;
    } while (nibble & 0x80000000);
    return value;
}