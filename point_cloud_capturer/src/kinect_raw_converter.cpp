#include "kinect_raw_converter.hpp"

void KinectRawConverter::convert_raw(uint16_t *depth, uint8_t *color, Vector3 *p_out, Color32 *c_out)
{
}

void KinectRawConverter::copy_calibration(KinectCalibration *cam_cal)
{

    for(int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            trafo[i][j] = cam_cal->trafo[i][j];
        }
    }
    align_to_depth = cam_cal->align_to_depth;
    cal.depth_camera_calibration = copy_camera(cam_cal->depth_camera_calibration);
    cal.color_camera_calibration = copy_camera(cam_cal->color_camera_calibration);
    cal.depth_mode = (k4a_depth_mode_t)cam_cal->depth_mode;
    cal.color_resolution = (k4a_color_resolution_t)cam_cal->color_resolution;

    for(int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            cal.extrinsics[i][j] = copy_extrensics(cam_cal->extrinsics[i][j]);
        }
    }
    // Create xy table
}

k4a_calibration_camera_t KinectRawConverter::copy_camera(kinect_cam_cal cam)
{
    return k4a_calibration_camera_t{
        .extrinsics = copy_extrensics(cam.extrinsics),
        .intrinsics = copy_intrinsics(cam.intrinsics),
        .resolution_width = cam.resolution_width,
        .resolution_height = cam.resolution_height,
        .metric_radius = cam.metric_radius
    };
}

k4a_calibration_extrinsics_t KinectRawConverter::copy_extrensics(kinect_cam_ex ex)
{
    return k4a_calibration_extrinsics_t{
        .rotation = {
            ex.rotation[0], ex.rotation[1], ex.rotation[2],
            ex.rotation[3], ex.rotation[4], ex.rotation[5],
            ex.rotation[6], ex.rotation[7], ex.rotation[8]
        },
        .translation = {
            ex.translation[0], ex.translation[1], ex.translation[2]
        }
    };
}

k4a_calibration_intrinsics_t KinectRawConverter::copy_intrinsics(kinect_cam_in in)
{
    return k4a_calibration_intrinsics_t{
        .type = (k4a_calibration_model_type_t)in.type,
        .parameter_count = in.parameter_count,
        .parameters = {
            in.parameters[0], in.parameters[1], in.parameters[2],
            in.parameters[3], in.parameters[4], in.parameters[5],
            in.parameters[6], in.parameters[7], in.parameters[8],
            in.parameters[9], in.parameters[10], in.parameters[11],
            in.parameters[12], in.parameters[13], in.parameters[14]
        }
    };
}
