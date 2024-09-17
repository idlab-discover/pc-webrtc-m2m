#pragma once
#include <vector>
#include <draco/point_cloud/point_cloud.h>
#include <draco/point_cloud/point_cloud_builder.h>
#include <draco/compression/decode.h>
#include "point_cloud.h"
enum DECODING_STATUS {
    D_Succes = 0,
    D_Fail = 1,
};
struct DecodedPointCloud {
    uint32_t n_points;
    float* points;
    uint8_t* colors;
    ~DecodedPointCloud() {
        delete[] points;
        delete[] colors;
    }
};
class DracoMDCDecoder {
    public:
        ~DracoMDCDecoder() {
            delete pc;
        }
        DecodedPointCloud* decode_pc(char *encoded_data, uint64_t size);
        unsigned int get_decoded_size() { return decoded_size; };
        uint32_t get_n_points() {return pc != nullptr ? pc->n_points : 0;}; 
	    float* get_point_array() {return pc->points;};
	    uint8_t* get_color_array() {return pc->colors;};
    private:
    
        unsigned int decoded_size;
        DECODING_STATUS status;
        DecodedPointCloud* pc = nullptr;
};

// Jobs vs threads
