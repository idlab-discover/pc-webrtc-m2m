#include "kinect/kinect_helper.hpp"
#ifdef USE_KINECT
 void KinectHelper::create_xy_table(const k4a_calibration_t &cal, bool align_to_depth, k4a_image_t &xy_table)
{
    k4a_result_t status;
    int width;
    int height;
    if(align_to_depth) {
        width = cal.depth_camera_calibration.resolution_width;
        height = cal.depth_camera_calibration.resolution_height;
    } else {
        width = cal.color_camera_calibration.resolution_width;
        height = cal.color_camera_calibration.resolution_height;
    }
    status = k4a_image_create(K4A_IMAGE_FORMAT_CUSTOM,
        width,
        height,
        width * (int)sizeof(k4a_float2_t),
        &xy_table
    );

    if (status != K4A_RESULT_SUCCEEDED) {
        Log::custom_log("create_xy_table: could not create xy table image", Default, LogColor::Red);
    }

    k4a_float2_t* table_data = (k4a_float2_t*)(void*)k4a_image_get_buffer(xy_table);

    k4a_float2_t p;
    k4a_float3_t ray;
    int valid;
    Log::custom_log(std::format("create_xy_table: align: {}, width: {}, height {}", align_to_depth, width, height), Default, LogColor::Orange);
    for (int y = 0, idx = 0; y < height; y++) {
        p.xy.y = (float)y;

        for (int x = 0; x < width; x++, idx++) {
            p.xy.x = (float)x; 
            if (align_to_depth) {
                k4a_calibration_2d_to_3d(
                    &cal, &p, 1.f, K4A_CALIBRATION_TYPE_DEPTH, K4A_CALIBRATION_TYPE_DEPTH, &ray, &valid
                );
            } else {
                k4a_calibration_2d_to_3d(
                    &cal, &p, 1.f, K4A_CALIBRATION_TYPE_COLOR, K4A_CALIBRATION_TYPE_COLOR, &ray, &valid
                );
            }
            if (valid) {
                table_data[idx].xy.x = ray.xyz.x;
                table_data[idx].xy.y = ray.xyz.y;
            } else {
                table_data[idx].xy.x = nanf("");
                table_data[idx].xy.y = nanf("");
            }
        }
    }

  }  
  #endif