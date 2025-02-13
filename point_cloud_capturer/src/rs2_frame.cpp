#include "rs2_frame.hpp"
#include <algorithm>

void RS2Frame::make_color_array(unsigned int width, unsigned height, unsigned int bpp, unsigned int sib, const uint8_t *texture)
{
    if(colors != nullptr) {
        delete colors;
    }
    colors = new Color[points.size()];
    vertices = new Vertex[points.size()];
    RS2Bounds bounds;
    bounds.min.x = bounds.min.y = bounds.min.z = std::numeric_limits<float>::max();
    bounds.max.x = bounds.max.y = bounds.max.z = std::numeric_limits<float>::lowest();

    auto tex_coords = points.get_texture_coordinates();
    auto rs_vertices = points.get_vertices();
    for(unsigned int i = 0; i < points.size(); i++) {
        auto& point = rs_vertices[i];
        if(point.z != 0.0f && point.z < 1.5f) {
            float u = tex_coords[i].u;
            float v = tex_coords[i].v;
            
            int texture_x = std::min(std::max(unsigned int(u * width + .5f), unsigned int (0)), width - 1);
            int texture_y = std::min(std::max(unsigned int(v * height + .5f), unsigned int (0)), height - 1);

            int bytes = texture_x * bpp;   // Get # of bytes per pixel
            int strides = texture_y * sib; // Get line width in bytes
            int tex_index = (bytes + strides);

            vertices[n_points] = {
                point.x,
                point.y,
                point.z
            };
            colors[n_points] = {
                texture[tex_index],
                texture[tex_index+1],
                texture[tex_index+2],
            };

            
            if (point.x < bounds.min.x) bounds.min.x = point.x;
            if (point.y < bounds.min.y) bounds.min.y = point.y;
            if (point.z < bounds.min.z) bounds.min.z = point.z;

            if (point.x > bounds.max.x) bounds.max.x = point.x;
            if (point.y > bounds.max.y) bounds.max.y = point.y;
            if (point.z > bounds.max.z) bounds.max.z = point.z;

            n_points++;
        }
    }
    x_offset = (bounds.min.x + bounds.max.x) / 2.0f;
    y_offset = (bounds.min.y + bounds.max.y) / 2.0f;
    z_offset = (bounds.min.z + bounds.max.z) / 2.0f;

    x_offset = 0.0;
    y_offset = 0.0;
    z_offset = 0.45;
}

void RS2Frame::make_raw_data_arrays(unsigned int width, unsigned int height, const rs2::depth_frame& depth_frame, const rs2::video_frame& color_frame) {
    const uint16_t* depth_ptr = static_cast<const uint16_t*>(depth_frame.get_data());
    const uint8_t* color_ptr = static_cast<const uint8_t*>(color_frame.get_data());
    raw_color.reserve(width*height*3);
    raw_depth.reserve(width*height);
    for(int i=0; i < width*height; i++) {
        if(depth_ptr[i] > 1500 || depth_ptr[i] == 0) {
            raw_depth[i]=0;
            raw_color[(i*3)+0]=0;
            raw_color[(i*3)+1]=0;
            raw_color[(i*3)+2]=0;
        } else {
            raw_depth[i]=depth_ptr[i];
            raw_color[(i*3)+0]=color_ptr[(i*3)+0];
            raw_color[(i*3)+1]=color_ptr[(i*3)+1];
            raw_color[(i*3)+2]=color_ptr[(i*3)+2];
            n_points++;
        }
    }
    //raw_depth.assign(depth_ptr, depth_ptr+width*height);
    //raw_color.assign(color_ptr, color_ptr+width*height*3);
}
