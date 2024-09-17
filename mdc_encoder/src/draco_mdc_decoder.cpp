#include "draco_mdc_decoder.hpp"

DecodedPointCloud* DracoMDCDecoder::decode_pc(char *encoded_data, uint64_t size)
{
    draco::Decoder decoder;
    draco::DecoderBuffer buf;
    
    buf.Init(encoded_data, size);
    auto dec_result = decoder.DecodePointCloudFromBuffer(&buf);
    if(!dec_result.ok()) {
        status = D_Fail;
    } else {
        auto& draco_pc = dec_result.value();
        const draco::PointAttribute *const point_att = draco_pc->GetNamedAttribute(draco::GeometryAttribute::POSITION);
        const draco::PointAttribute *const color_att = draco_pc->GetNamedAttribute(draco::GeometryAttribute::COLOR);
        float* coords = new float[draco_pc->num_points()*3];
        uint8_t* colors = new uint8_t[draco_pc->num_points()*3];
        for (draco::PointIndex i(0); i < draco_pc->num_points(); ++i) {
            const draco::AttributeValueIndex point_index = point_att->mapped_index(i);
            const draco::AttributeValueIndex color_index = color_att->mapped_index(i);
            point_att->ConvertValue(point_index, 3, coords+i.value()*3);
            color_att->ConvertValue(color_index, 3, colors+i.value()*3);
        }
        pc = new DecodedPointCloud{
            draco_pc->num_points(),
            coords,
            colors,
        };
        status = D_Succes;
    }
    return pc;
}