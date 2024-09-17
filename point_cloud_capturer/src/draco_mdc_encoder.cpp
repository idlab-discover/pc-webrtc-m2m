#include "draco_mdc_encoder.hpp"
#include <draco/point_cloud/point_cloud.h>
#include <draco/point_cloud/point_cloud_builder.h>
#include <draco/compression/encode.h>
#include "description.h"
void DracoMDCEncoder::encode_pc(Description *pc)
{
    draco::PointCloudBuilder builder;
    builder.Start(pc->n_points);
    // 3 bytes for position and 1 byte for color
    const int32_t att_id_pos = builder.AddAttribute(draco::GeometryAttribute::POSITION, 3, draco::DT_FLOAT32);
    const int32_t att_id_col = builder.AddAttribute(draco::GeometryAttribute::COLOR, 3, draco::DT_UINT8);

    // Set attributes using point cloud values
    for (int i = 0; i < pc->n_points; ++i) {
        builder.SetAttributeValueForPoint(att_id_pos, draco::PointIndex(i), &pc->coords[i]);
        builder.SetAttributeValueForPoint(att_id_col, draco::PointIndex(i), &pc->colors[i]);
    }
    std::unique_ptr<draco::PointCloud> draco_pc = builder.Finalize(false);
    draco::Encoder encoder;
    encoder.SetEncodingMethod(draco::POINT_CLOUD_KD_TREE_ENCODING);
    // Can potentially change quantization here
    encoder.SetAttributeQuantization(draco::GeometryAttribute::POSITION, 11);
    
    if (!encoder.EncodePointCloudToBuffer(*draco_pc, &buffer).ok()) {
        status = ENCODING_STATUS::S_Fail;
        encoded_size = 0;
    } else {
        status = ENCODING_STATUS::S_Succes;
        encoded_size = buffer.size();
    }
   
    
}