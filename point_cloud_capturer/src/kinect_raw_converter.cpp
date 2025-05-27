#include "kinect_raw_converter.hpp"

void KinectRawConverter::convert_raw(uint16_t *depth, uint8_t *color, Vector3 *p_out, Color32 *c_out)
{
    k4a_float2_t* xy_table_data = (k4a_float2_t*)(void*)k4a_image_get_buffer(xy_table);
    unsigned char* color_buffer = reinterpret_cast<unsigned char*>(color);
    int n_points = 0;
    for (int i = 0; i < width * height; i++) {
        bool is_filtered = false;
        
        if (depth[i] != 0 && !isnan(xy_table_data[i].xy.x) && !isnan(xy_table_data[i].xy.y)) {
                c_out[n_points] = Color32{color_buffer[i * 3], color_buffer[i * 3 + 1], color_buffer[i * 3 + 2], 255};
                copy_point(p_out[n_points], xy_table_data[i], trafo, depth[i]);
                n_points++;
        }      
    }   
}

inline void KinectRawConverter::copy_point(Vector3& p, const k4a_float2_t& xy_table_data,  const float (&trafo)[4][4], uint16_t depth)
{
    p.x = xy_table_data.xy.x * (float)depth;
    p.y = xy_table_data.xy.y * (float)depth;
    p.z = (float)depth;
    float x = p.x / 1000.0;
    float y = p.y / 1000.0;
    float z = p.z / 1000.0;
    p.x = trafo[0][0]*x + trafo[0][1]*y + trafo[0][2]*z + trafo[0][3];
    p.y = trafo[1][0]*x + trafo[1][1]*y + trafo[1][2]*z + trafo[1][3];
    p.z = trafo[2][0]*x + trafo[2][1]*y + trafo[2][2]*z + trafo[2][3];
}


void KinectRawConverter::copy_calibration(KinectCalibration *cam_cal)
{

    for(int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            trafo[i][j] = cam_cal->trafo[i][j];
            Log::custom_log(std::format("copy_calibration: trafo: {} {} {}", i, j, trafo[i][j]), Default, LogColor::Orange);
        }
    }
    align_to_depth = cam_cal->align_to_depth;
    cal.depth_camera_calibration = copy_camera(cam_cal->depth_camera_calibration);
    cal.color_camera_calibration = copy_camera(cam_cal->color_camera_calibration);
    cal.depth_mode = (k4a_depth_mode_t)cam_cal->depth_mode;
    cal.color_resolution = (k4a_color_resolution_t)cam_cal->color_resolution;
    depth_width = cal.depth_camera_calibration.resolution_width;
    depth_height = cal.depth_camera_calibration.resolution_height;
    color_width = cal.color_camera_calibration.resolution_width;
    color_height = cal.color_camera_calibration.resolution_height;
    if(align_to_depth) {
        width = depth_width;
        height = depth_height;
    } else {
        width = color_width;
        height = color_height;
    }

    for(int i = 0; i < 4; i++) {
        for (int j = 0; j < 4; j++) {
            cal.extrinsics[i][j] = copy_extrensics(cam_cal->extrinsics[i][j]);
        }
    }
    Log::custom_log(std::format("copy_calibration: align: {}, width: {}, height {}", align_to_depth, width, height), Default, LogColor::Orange);
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
