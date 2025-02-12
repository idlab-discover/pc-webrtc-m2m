#include "jpeg_decoder.hpp"

JpegDecoder::JpegDecoder() {
    decompressor = tjInitDecompress();

}
JpegDecoder::~JpegDecoder() {
    if(decompressor != nullptr) {
        tjDestroy(decompressor);
    }
    while(!buffers.empty()) {
        delete[] buffers.top();
        buffers.pop();
    }
}
DecodedJpeg* JpegDecoder::decompress_frame(unsigned char* encoded_jpeg, unsigned long size, unsigned int width, unsigned int height) {
    std::unique_lock lk(m_enqueue);
    int t_width;
    int t_height;
    int subsamp;
    if(buffers.empty()) {
        buffers.push(new unsigned char[width*height*3]);
    }
    unsigned char* buffer = buffers.top();
    buffers.pop();
    tjDecompressHeader2(decompressor, encoded_jpeg, size, &t_width, &t_height, &subsamp);
    tjDecompress2(decompressor, encoded_jpeg, size, buffer, width, 0/*pitch*/, height, TJPF_RGB, TJFLAG_FASTDCT);
    return new DecodedJpeg(width, height, buffer, this);
};
void JpegDecoder::return_buffer(unsigned char* buffer) {
    std::unique_lock lk(m_enqueue);
    buffers.push(buffer);
}