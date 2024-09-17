#include "rs2_frame.hpp"
#include <algorithm>

void RS2Frame::make_color_array(unsigned int width, unsigned height, unsigned int bpp, unsigned int sib, const uint8_t *texture)
{
    if(colors != nullptr) {
        delete colors;
    }
    colors = new Color[points.size()];
    RS2Bounds bounds;
    bounds.min.x = bounds.min.y = bounds.min.z = std::numeric_limits<float>::max();
    bounds.max.x = bounds.max.y = bounds.max.z = std::numeric_limits<float>::lowest();

    auto tex_coords = points.get_texture_coordinates();
    auto vertices = points.get_vertices();
    for(unsigned int i = 0; i < points.size(); i++) {
        float u = tex_coords[i].u;
        float v = tex_coords[i].v;
        
        int texture_x = std::min(std::max(unsigned int(u * width + .5f), unsigned int (0)), width - 1);
        int texture_y = std::min(std::max(unsigned int(v * height + .5f), unsigned int (0)), height - 1);

        int bytes = texture_x * bpp;   // Get # of bytes per pixel
        int strides = texture_y * sib; // Get line width in bytes
        int tex_index = (bytes + strides);
        colors[i] = {
            texture[tex_index],
            texture[tex_index+1],
            texture[tex_index+2],
        };

        auto& point = vertices[i];
        if (point.x < bounds.min.x) bounds.min.x = point.x;
        if (point.y < bounds.min.y) bounds.min.y = point.y;
        if (point.z < bounds.min.z) bounds.min.z = point.z;

        if (point.x > bounds.max.x) bounds.max.x = point.x;
        if (point.y > bounds.max.y) bounds.max.y = point.y;
        if (point.z > bounds.max.z) bounds.max.z = point.z;

    }
    x_offset = (bounds.min.x + bounds.max.x) / 2.0f;
    y_offset = (bounds.min.y + bounds.max.y) / 2.0f;
    z_offset = (bounds.min.z + bounds.max.z) / 2.0f;

    x_offset = 0.0;
    y_offset = 0.0;
    z_offset = 0.45;
}


