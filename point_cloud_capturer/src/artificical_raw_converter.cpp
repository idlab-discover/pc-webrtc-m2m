#include "artificical_raw_converter.hpp"

void ArtificalRawConverter::convert_raw(uint16_t *depth, uint8_t *color, Vector3 *p_out, Color32 *c_out)
{
    unsigned int p = 0;
    unsigned int a_p = 0;
    for(int k=0; k < side_size; k++) {
        for(int i=0; i < side_size; i++) {
            for(int j=0; j < side_size; j++) {
                if(depth[p] != 0) {
                    p_out[a_p] = Vector3{(float)i, (float)j, (float)depth[p]};
                    c_out[a_p]= Color32{color[(p*3)], color[(p*3)+1], color[(p*3)+2], 255};
                    a_p++;
                }           
                p++;
            }
        }
    }
}