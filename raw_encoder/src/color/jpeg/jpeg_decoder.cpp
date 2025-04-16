#include "color/jpeg/jpeg_decoder.hpp"

JpegDecoder::JpegDecoder() : ColorDecoder() {
    decompressor = tjInitDecompress();

}
JpegDecoder::~JpegDecoder() {
    if(decompressor != nullptr) {
        tjDestroy(decompressor);
    }
}
DecodedColor* JpegDecoder::decompress_frame(unsigned char* encoded_jpeg, unsigned long size, unsigned int width, unsigned int height) {
    std::unique_lock lk(m_enqueue);
    unsigned char* buff = get_buffer(width, height);
    int t_width, t_height;
    int subsamp;
    tjDecompressHeader2(decompressor, encoded_jpeg, size, &t_width, &t_height, &subsamp);
    tjDecompress2(decompressor, encoded_jpeg, size, buff, width, 0/*pitch*/, height, TJPF_RGB, TJFLAG_FASTDCT);
    return new DecodedColor(width, height, buff, this);
};
