#pragma once
#include <librealsense2/rs.hpp>
#include "framework.h"
#include "raw_converter.hpp"
#include "capturer.hpp"
#include "log.h"
class ArtificalRawConverter : public RawConverter {
    public:
        ArtificalRawConverter(void* cal) : RawConverter(cal) {
            side_size = static_cast<ArtificialCalibration*>(cal)->side_size;
            Log::custom_log("ArtificalRawConverter: Creating artificical raw converter with side size: " + std::to_string(side_size), Default, LogColor::Orange);
            // frame_buffer.start_buffer();
        };
        ~ArtificalRawConverter() {
           // frame_buffer.stop_buffer();
        }
        virtual void convert_raw(uint16_t* depth, uint8_t* color, Vector3* p_out, Color32* c_out);
    private:
        unsigned int side_size;
};