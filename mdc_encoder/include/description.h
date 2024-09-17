#pragma once
#include <cstdint>
#include "point_cloud.h"
#include "draco_mdc_encoder.hpp"
struct Description {
    unsigned int frame_nr;
    unsigned int description_nr;
    unsigned int n_points_in_total;
    unsigned int n_points;
    unsigned int current_points = 0;
    Vertex* coords;
    Color* colors;
    DracoMDCEncoder* enc;
    ~Description() {
        delete[] coords;
        delete[] colors;
        delete enc;
    }
};