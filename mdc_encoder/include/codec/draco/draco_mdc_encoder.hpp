#pragma once
#include <vector>
#include <draco/point_cloud/point_cloud.h>
#include <draco/point_cloud/point_cloud_builder.h>
#include <draco/compression/encode.h>
#include "point_cloud.h"

class Description;
enum ENCODING_STATUS {
    S_Succes = 0,
    S_Fail = 1,
};
class DracoMDCEncoder {
    public:
        void encode_pc(Description* pc);
        unsigned int get_encoded_size() { return encoded_size; };
        char* get_raw_data() { return encoded_size != 0 ? const_cast<char*>(buffer.data()) : nullptr;};
    private:
        std::vector<unsigned int> calculate_indices(unsigned int n_points);
        draco::EncoderBuffer buffer;
        unsigned int encoded_size = 0;
        ENCODING_STATUS status;
};

// Jobs vs threads
