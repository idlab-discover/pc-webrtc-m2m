#include "kinect_frame.hpp"

void KinectFrame::make_pc(const k4a_image_t &xy_table, const float (&trafo)[4][4])
{
    colors.reserve(color_width * color_height / 6);
    vertices.reserve(color_width * color_height / 6);

    uint16_t* depth_data = (uint16_t*)k4a_image_get_buffer(depth_image);
    uint8_t* color_buffer = k4a_image_get_buffer(color_image);
    k4a_float2_t* xy_table_data = (k4a_float2_t*)(void*)k4a_image_get_buffer(xy_table);

    for (int i = 0; i < color_width * color_height; i++) {
        bool is_filtered = false;
        
        if (depth_data[i] != 0 && !isnan(xy_table_data[i].xy.x) && !isnan(xy_table_data[i].xy.y)) {
            Color color;
            Vertex vertex = get_transformed_vertex(xy_table_data[i], trafo, depth_data[i]);
          
            if(!is_vertex_filtered(vertex)) {
                colors.push_back(Color{color_buffer[i * 4 + 2], color_buffer[i * 4 + 1], color_buffer[i * 4]});
                vertices.push_back(vertex);
                n_points++;
            } 
            
        }      
    }
}

void KinectFrame::make_raw_data_arrays(const k4a_image_t& xy_table, const float (&trafo)[4][4], FrameCleanupSettings cleanup_settings)
{
    // TODO Change it so this only needs to happen for JPEG; WebP has functions for this anyays
    // And also prevents the need to change it to RGB from BGR
    raw_color.reserve(color_width*color_height*3);
    apply_depth_filter_to_raw(xy_table, trafo, cleanup_settings);
    
}

void KinectFrame::apply_depth_filter_to_raw(const k4a_image_t& xy_table, const float (&trafo)[4][4], FrameCleanupSettings cleanup_settings)
{
    uint16_t* depth_data = ((uint16_t*)k4a_image_get_buffer(depth_image));
    uint8_t* color_buffer = (k4a_image_get_buffer(color_image));
    k4a_float2_t* xy_table_data = (k4a_float2_t*)(void*)k4a_image_get_buffer(xy_table);

    unsigned int block_size =8;
    unsigned int half_block = block_size*block_size/2;
    unsigned int fith_block = block_size*block_size/5;
    for (int row = 0; row <= color_height - block_size; row += block_size) {
        for (int col = 0; col <= color_width - block_size; col += block_size) {
            bool is_zero_block = true;
            bool is_avg_block = false;
            unsigned int n_zero_points = 0;
           
            // Check the 8x8 block
            for (int dy = 0; dy < block_size; ++dy) {
                int row_y = (row + dy) * color_width;
                for (int dx = 0; dx < block_size; ++dx) {
                    int row_x = (col + dx);
                    Vertex vertex = get_transformed_vertex(xy_table_data[row_y + row_x], trafo, depth_data[row_y + row_x]);
                    if (!is_vertex_filtered(vertex)) {
                        is_zero_block = false;
                        n_points++;
                    } else {
                        n_zero_points++;
                        depth_data[row_y + row_x] = 0;
                    }
                }
            }
       
            for (int dy = 0; dy < block_size; ++dy) {
                for (int dx = 0; dx < block_size; ++dx) {
                    int i_index = ((row + dy) * color_width + (col + dx))*3;
                    int i_index_ori = ((row + dy) * color_width + (col + dx))*4;
                    if(is_zero_block) {
                        raw_color[i_index+0] = 0;
                        raw_color[i_index+1] = 0;
                        raw_color[i_index+2] = 0;
                    } else {
                        raw_color[i_index+0] = color_buffer[i_index_ori+2];
                        raw_color[i_index+1] = color_buffer[i_index_ori+1];
                        raw_color[i_index+2] = color_buffer[i_index_ori+0];
                    }
                    
                }
            }
            
            
            
        }
    }
}

inline Vertex KinectFrame::get_transformed_vertex(const k4a_float2_t& xy_table_data,  const float (&trafo)[4][4], uint16_t depth)
{
    Vertex vertex;
    vertex.x = xy_table_data.xy.x * (float)depth;
    vertex.y = xy_table_data.xy.y * (float)depth;
    vertex.z = (float)depth;
    float x = vertex.x / 1000.0;
    float y = vertex.y / 1000.0;
    float z = vertex.z / 1000.0;
    vertex.x = trafo[0][0]*x + trafo[0][1]*y + trafo[0][2]*z + trafo[0][3];
    vertex.y = trafo[1][0]*x + trafo[1][1]*y + trafo[1][2]*z + trafo[1][3];
    vertex.z = trafo[2][0]*x + trafo[2][1]*y + trafo[2][2]*z + trafo[2][3];
    return vertex;
}

inline bool KinectFrame::is_vertex_filtered(const Vertex &vertex)
{
    if (vertex.y < 0.02 || vertex.y > 2.2) {
        return true;
    }
    

    float distance_2 = vertex.x * vertex.x + vertex.z * vertex.z;
    if (!(distance_2 < 1.25f * 1.25f)) {
        return true;
    } 

    return false;
}

