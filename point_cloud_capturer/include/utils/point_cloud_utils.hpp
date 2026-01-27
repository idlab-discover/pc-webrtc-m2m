#pragma once

#include "point_cloud.hpp"
namespace pcutils {
    PointCloud* downsample_pc_random(PointCloud* pc, unsigned int max_points, bool create_new_pc);
    bool save_pc_to_ply(const char* file_path, PointCloud* pc);
}
