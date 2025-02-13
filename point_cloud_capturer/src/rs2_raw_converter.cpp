#include "rs2_raw_converter.hpp"
#include <algorithm>
RS2RawConverter::RS2RawConverter(CapturerIntrinsics depth_intrinsics, CapturerIntrinsics color_intrinsics) 
    : depth_sensor(dev.add_sensor("Depth")), color_sensor(dev.add_sensor("Color")), RawConverter(depth_intrinsics.width, depth_intrinsics.height) 
{
    rs2_intrinsics d_int = {
        (int)depth_intrinsics.width, (int)depth_intrinsics.height, depth_intrinsics.ppx, depth_intrinsics.ppy, depth_intrinsics.fx, depth_intrinsics.fy, 
        static_cast<rs2_distortion>(depth_intrinsics.model), 
        {depth_intrinsics.coeffs[0], depth_intrinsics.coeffs[1], depth_intrinsics.coeffs[2], depth_intrinsics.coeffs[3], depth_intrinsics.coeffs[4]} 
    };
    rs2_intrinsics c_int = {
        (int)color_intrinsics.width, (int)color_intrinsics.height, color_intrinsics.ppx, color_intrinsics.ppy, color_intrinsics.fx, color_intrinsics.fy, 
        static_cast<rs2_distortion>(color_intrinsics.model), 
        {color_intrinsics.coeffs[0], color_intrinsics.coeffs[1], color_intrinsics.coeffs[2], color_intrinsics.coeffs[3], color_intrinsics.coeffs[4]} 
    };
    dev.create_matcher(RS2_MATCHER_DEFAULT);
    depth_stream = depth_sensor.add_video_stream({
        RS2_STREAM_DEPTH, 0, 0, (int)width, (int) height, 30, 2, RS2_FORMAT_Z16, d_int
    });
    color_stream = color_sensor.add_video_stream({
        RS2_STREAM_COLOR, 0, 1, (int)width, (int) height, 30, 3, RS2_FORMAT_RGB8, c_int
    });
    depth_sensor.add_read_only_option(RS2_OPTION_DEPTH_UNITS, 0.001f);
    depth_sensor.add_read_only_option(RS2_OPTION_STEREO_BASELINE, 0.001f);

    depth_stream.register_extrinsics_to(color_stream, {{ 1, 0, 0, 0, 1, 0, 0, 0, 1 }, {0, 0, 0}});   
    
    depth_sensor.open(depth_stream);
    color_sensor.open(color_stream);
    
    depth_sensor.start(sync);
    color_sensor.start(sync);
}

RS2RawConverter::~RS2RawConverter() {
    depth_sensor.stop();
    color_sensor.stop();
    depth_sensor.close();
    color_sensor.close();
}

void RS2RawConverter::convert_raw(uint16_t *depth, uint8_t *color, Vector3 *p_out, Color32 *c_out)
{
    size_t n_frames = 0;
    unsigned int tries = 0;
    rs2::frameset fs;
    
    while(n_frames == 0 && tries < 5) {
        depth_sensor.on_video_frame({ (void*)depth, // Frame pixels from capture API
            [](void*) {}, // Custom deleter (if required)
            2 * (int)848, 2, // Stride and Bytes-per-pixel
            double(internal_frame_number * 33), RS2_TIMESTAMP_DOMAIN_SYSTEM_TIME,
            internal_frame_number, // Timestamp, Frame# for potential sync services
            depth_stream, 0.001f});
        color_sensor.on_video_frame({ (void*)color, // Frame pixels from capture API
            [](void*) {}, // Custom deleter (if required)
            3 * (int)848, 3, // Stride and Bytes-per-pixel
            double(internal_frame_number * 33), RS2_TIMESTAMP_DOMAIN_SYSTEM_TIME,
            internal_frame_number, // Timestamp, Frame# for potential sync services
            color_stream });
        fs = sync.wait_for_frames();
        n_frames = fs.size();
        tries++;
        internal_frame_number++;
    }
    if(n_frames == 2) {
        rs2::depth_frame depth_frame = fs.get_depth_frame();
        rs2::video_frame color_frame = fs.get_color_frame();
        pc.map_to(color_frame);
        rs2::points points = pc.calculate(depth_frame);
        
        auto tex_coords = points.get_texture_coordinates();
        auto vertices = points.get_vertices();
        int bpp = color_frame.get_bytes_per_pixel();
        int sib = color_frame.get_stride_in_bytes();
        const uint8_t* texture = static_cast<const uint8_t*>(color_frame.get_data());
        auto vert = points.get_vertices();
       // points.export_to_ply("test.ply", color_frame);
        //std::copy(points.get_vertices(), points.get_vertices()+points.size(), reinterpret_cast<rs2::vertex*>(p_out)); // Copy all points at once because the struct has the same composition
        unsigned int ap = 0;
        for(unsigned int i = 0; i < points.size(); i++) {
            float u = tex_coords[i].u;
            float v = tex_coords[i].v;
            
            int texture_x = (std::min)((std::max)(unsigned int(u * width + .5f), unsigned int (0)), width - 1);
            int texture_y = (std::min)((std::max)(unsigned int(v * height + .5f), unsigned int (0)), height - 1);

            int bytes = texture_x * bpp;   // Get # of bytes per pixel
            int strides = texture_y * sib; // Get line width in bytes
            int tex_index = (bytes + strides);
            if(vert[i].z != 0.0f) {
                p_out[ap] = Vector3{vert[i].x*-1.0f, vert[i].y*-1.0f, vert[i].z*-1.0f};
                c_out[ap] = Color32{
                    texture[tex_index],
                    texture[tex_index+1],
                    texture[tex_index+2],
                    255,
                };
                ap++;
            }
        }
    }
    
}