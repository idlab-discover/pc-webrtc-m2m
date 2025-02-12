#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "raw_converter.hpp"

class ArtificalRawConverter : public RawConverter {
    public:
        ArtificalRawConverter(unsigned int side_size) : RawConverter(side_size, side_size), side_size(side_size) {
        };
        ~ArtificalRawConverter() {
           // frame_buffer.stop_buffer();
        }
        virtual void convert_raw(uint16_t* depth, uint8_t* color, Vector3* p_out, Color32* c_out);
    private:
        unsigned int side_size;
};