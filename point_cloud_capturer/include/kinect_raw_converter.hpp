#pragma once
#ifdef USE_KINECT
#include "framework.h"
#include "raw_converter.hpp"
#include <k4a/k4a.h>
#include <k4arecord/playback.h>
#include "capturer.hpp"
#include "kinect/kinect_helper.hpp"
class KinectRawConverter : public RawConverter {
    public:
        KinectRawConverter(void* cal_ori) : RawConverter(cal_ori) {
            copy_calibration(static_cast<KinectCalibration*>(cal_ori));
            KinectHelper::create_xy_table(cal, align_to_depth, xy_table);
        };
        ~KinectRawConverter() {
           // frame_buffer.stop_buffer();
        }
        virtual void convert_raw(uint16_t* depth, uint8_t* color, Vector3* p_out, Color32* c_out);
    private:
        unsigned int side_size;
        float trafo[4][4];
        k4a_image_t xy_table = NULL;
        unsigned int width;
        unsigned int height;
        unsigned int depth_width;
        unsigned int depth_height;
        unsigned int color_width;
        unsigned int color_height;
        
        float min_height;
        float max_height;
        float radius;
        bool align_to_depth;

        k4a_calibration_t cal;

        inline void copy_point(Vector3& p, const k4a_float2_t& xy_table_data,  const float (&trafo)[4][4], uint16_t depth);

        void copy_calibration(KinectCalibration* cam_cal);
        void fill_extrensics(KinectCalibration* kin_cal);
        k4a_calibration_camera_t copy_camera(kinect_cam_cal cam);
        k4a_calibration_extrinsics_t copy_extrensics(kinect_cam_ex ex);
        k4a_calibration_intrinsics_t copy_intrinsics(kinect_cam_in in);
};
#endif