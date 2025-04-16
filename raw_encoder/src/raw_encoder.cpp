#include "raw_encoder.hpp"
#include <fstream>
EncodedRaw* RawEncoder::encode_raw(RawFrame* pc) {
    EncodedColor* enc_color = col->compress_frame(pc->color);
    if(pc->frame_nr == 250) {
        std::ofstream file("raw.webp", std::ios::binary);
        if (file.is_open()) {
            file.write(reinterpret_cast<char*>(enc_color->get_bytes()), enc_color->get_size());
            file.close();
        } 
    }
    
    EncodedDepth* enc_depth = dep->encode_depth(pc->depth);
    return new EncodedRaw(pc->timestamp, pc->frame_nr, width, height, pc->n_points, enc_depth, enc_color);
}