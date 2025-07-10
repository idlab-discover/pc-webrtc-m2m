#include "raw_encoder.hpp"
#include "log.h"
#include <format>
#include <fstream>
#include "logging/logging_macros.hpp"
EncodedRaw* RawEncoder::encode_raw(RawFrame* pc) {
    LOG_FRAME_STATUS_TO_FILE_LIMITED(NAME, Log::Status::StartEncodingColor, pc->capturer_id, pc->frame_nr);
  
    EncodedColor* enc_color = col->compress_frame(pc->color);
    
    LOG_FRAME_STATUS_SIZE_TO_FILE_LIMITED(NAME, Log::Status::EndEncodingColor, pc->capturer_id, pc->frame_nr, enc_color->get_size());
    LOG_FRAME_STATUS_TO_FILE_LIMITED(NAME, Log::Status::StartEncodingDepth, pc->capturer_id, pc->frame_nr);
    
    EncodedDepth* enc_depth = dep->encode_depth(pc->depth);

    LOG_FRAME_STATUS_SIZE_TO_FILE_LIMITED(NAME, Log::Status::EndEncodingDepth, pc->capturer_id, pc->frame_nr, enc_depth->get_size());
    return new EncodedRaw(pc->timestamp, pc->capturer_id, pc->frame_nr, width, height, pc->n_points, enc_depth, enc_color);
}