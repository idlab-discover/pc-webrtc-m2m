#include "rs2_frame.hpp"
#include <algorithm>

void RS2Frame::make_color_array(unsigned int bpp, unsigned int sib, const uint8_t *texture)
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

void RS2Frame::make_raw_data_arrays(
    const rs2::depth_frame& depth_frame, const rs2::video_frame& color_frame,
    FrameCleanupSettings cleanup_settings
) {
    const uint16_t* depth_ptr = static_cast<const uint16_t*>(depth_frame.get_data());
    const uint8_t* color_ptr = static_cast<const uint8_t*>(color_frame.get_data());
    raw_color.reserve(width*height*3);
    raw_depth.reserve(width*height);
    cleanup_settings.blackout_block_size = 8;
    cleanup_settings.should_blackout = true;
    cleanup_settings.should_cleanup_depth = true;
    cleanup_settings.should_apply_depth_filter = true;
  
    if(cleanup_settings.should_apply_depth_filter) {
        apply_depth_filter_to_raw(depth_ptr, color_ptr, cleanup_settings);
    }
    
    //raw_depth.assign(depth_ptr, depth_ptr+width*height);
    //raw_color.assign(color_ptr, color_ptr+width*height*3);
}

void RS2Frame::apply_depth_filter_to_raw(
    const uint16_t* rs_depth, const uint8_t* rs_color, FrameCleanupSettings cleanup_settings
) {
    unsigned int block_size = cleanup_settings.blackout_block_size;
    unsigned int half_block = block_size*block_size/2;
    unsigned int fith_block = block_size*block_size/5;
    for (int row = 0; row <= height - block_size; row += block_size) {
        for (int col = 0; col <= width - block_size; col += block_size) {
            bool is_zero_block = true;
            bool is_avg_block = false;
            unsigned int n_zero_points = 0;
           
            // Check the 8x8 block
            for (int dy = 0; dy < block_size; ++dy) {
                int row_y = (row + dy) * width;
                for (int dx = 0; dx < block_size; ++dx) {
                    int row_x = (col + dx);
                    if (rs_depth[row_y + row_x] > 0 && rs_depth[row_y + row_x] < 1500) {
                        is_zero_block = false;
                        int i_index = (row_y + (row_x))*3;
                        raw_depth[row_y + row_x] = rs_depth[row_y + row_x];
                        n_points++;
                    } else {
                        if(cleanup_settings.should_cleanup_depth) {
                            // TODO make better / more cleanup methods
                            if(row_x > 0 && row_x < width - 1) {
                                unsigned short left = rs_depth[row_y + row_x-1];
                                unsigned short right = rs_depth[row_y + row_x+1];
                                if(left != 0 && right != 0 && right < 1500) {
                                    raw_depth[row_y + row_x] = (left + right) / 2;
                                     // TODO also repair color
                                    is_zero_block = false;
                                    n_points++;
                                    continue;
                                }
                            }
                        }
                        n_zero_points++;
                        raw_depth[row_y + row_x] = 0;
                 
                    }
                }
            }
            // If block to the left is zero
            // and this block is more than 50% zero = zet whole block to zero
            // or this block is less than 50% zero = set whole block to average value
            // (less than 50 but more than 20%)
            // Below 20% fix each black pixel separately
            //
            unsigned int avg_r = 0;
            unsigned int avg_g = 0;
            unsigned int avg_b = 0;
            if(n_zero_points >= fith_block && !is_zero_block) {
                is_avg_block = true;
              //  is_zero_block = true;
                // Make whole block average color
                // Calculate average r,g,b values of block
                for (int dy = 0; dy < block_size; ++dy) {
                    int row_y = (row + dy) * width;
                    for (int dx = 0; dx < block_size; ++dx) {
                        int row_x = (col + dx);
                        int i_index = (row_y + row_x)*3;
                        if(rs_depth[row_y + row_x] > 0 && rs_depth[row_y + row_x] < 1500) {
                            avg_r += rs_color[i_index+0];
                            avg_g += rs_color[i_index+1];
                            avg_b += rs_color[i_index+2];
                        }
                    }
                }
                avg_r /= (block_size*block_size-n_zero_points);
                avg_g /= (block_size*block_size-n_zero_points);
                avg_b /= (block_size*block_size-n_zero_points);
            } else if(n_zero_points > 0 && n_zero_points < fith_block) {
                is_zero_block = false;
                // Repair block => repair each pixel separately
                // Set color value to average surrounding pixels
                
            }
            for (int dy = 0; dy < block_size; ++dy) {
                for (int dx = 0; dx < block_size; ++dx) {
                    int i_index = ((row + dy) * width + (col + dx))*3;
                    uint8_t r = 0;
                    uint8_t g = 0;
                    uint8_t b = 0;
                    if(!is_zero_block) {
                        r = rs_color[i_index+0];
                        g = rs_color[i_index+1];
                        b = rs_color[i_index+2];
                    } else if(is_avg_block){
                  //      r = avg_r;
                   //     g = avg_g;
                     //   b = avg_b;
                    }
                    raw_color[i_index+0] = r;
                    raw_color[i_index+1] = g;
                    raw_color[i_index+2] = b;
                }
            }
            
        }
    }
}