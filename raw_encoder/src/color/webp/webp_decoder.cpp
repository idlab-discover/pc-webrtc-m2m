#include "color/webp/webp_decoder.hpp"
#include "color/webp/decoded_webp.hpp"
// Constructor
WebPDecoder::WebPDecoder() {
    // Initialization logic if needed
    WebPInitDecoderConfig(&config);
    config.options.use_threads = 1; // Enable multi-threading
    config.output.colorspace = MODE_RGB; // Set output colorspace
    config.output.is_external_memory = 1; // Use external memory
}

// Destructor
WebPDecoder::~WebPDecoder() {
    // Cleanup logic if needed
}

// decompress_frame function
DecodedColor* WebPDecoder::decompress_frame(unsigned char* encoded_jpeg, unsigned long size, unsigned int width, unsigned int height) {
    std::unique_lock<std::mutex> lk(m_enqueue);
    unsigned char* buffer = get_buffer(width, height);
    int t_height, t_width;
    uint8_t* decoded_data = WebPDecodeRGBInto(encoded_jpeg, size, buffer, width*height*3, width*3);
    WebPFreeDecBuffer(&config.output); // Free the output buffer
    return new DecodedColor(width, height, buffer, this);
}
