#include "raw_encoder.hpp"

EncodedRaw* RawEncoder::encode_raw(RawFrame* pc) {
    EncodedJpeg* enc_color = jpg.compress_frame(pc->color);
    EncodedDepth* enc_depth = dep.encode_depth(pc->depth);
    return new EncodedRaw(pc->timestamp, pc->frame_nr, width, height, pc->n_points, enc_depth, enc_color);
}