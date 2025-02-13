#pragma once
#include <librealsense2/rs.hpp>
#include <librealsense2/hpp/rs_internal.hpp>
#include "framework.h"
#include "raw_converter.hpp"
#include "capturer.hpp"

class RS2RawConverter : public RawConverter {
    public:
        RS2RawConverter(CapturerIntrinsics depth_intrinsics, CapturerIntrinsics color_intrinsics);
        ~RS2RawConverter();
        virtual void convert_raw(uint16_t* depth, uint8_t* color, Vector3* p_out, Color32* c_out);
    private:
        rs2::software_device dev;
        rs2::software_sensor depth_sensor;
        rs2::software_sensor color_sensor;
        rs2::stream_profile depth_stream;
        rs2::stream_profile color_stream;
        rs2::syncer sync;
        rs2::pointcloud pc;
        int internal_frame_number = 0;
};