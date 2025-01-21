#pragma once
#include <cstdint>
#include "frame.hpp"
#include "point_cloud_data.h"
struct PointCloud {
    uint64_t timestamp;
    unsigned int frame_nr;
    unsigned int n_points;
    Vertex* coords;
    Color* colors;
    Frame* frame_pointer = nullptr; // DO NOT USE OR FREE YOURSELF!

    ~PointCloud() {
        delete frame_pointer;
    };
};