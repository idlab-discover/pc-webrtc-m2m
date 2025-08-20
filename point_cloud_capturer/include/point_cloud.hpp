#pragma once
#include <cstdint>
#include "frame.hpp"
#include "point_cloud_data.h"
struct PointCloud {
    uint64_t timestamp;
    unsigned int capturer_id;
    unsigned int frame_nr;
    unsigned int n_points;
    Vertex* coords;
    Color* colors;
    Frame* frame_pointer = nullptr; // DO NOT USE OR FREE YOURSELF!
    bool delete_arrays = false;
    ~PointCloud() {
        if(frame_pointer != nullptr) {
            delete frame_pointer; // This will free the frame pointer if it was allocated
        }
        if(delete_arrays) {
            delete[] coords;
            delete[] colors;
        }
    };
};