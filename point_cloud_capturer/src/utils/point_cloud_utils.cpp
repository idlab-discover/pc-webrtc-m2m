#include "point_cloud_utils.hpp"
#include <random>
#include <numeric> 
#include "external/plywoot/plywoot.hpp"
#include <fstream>
namespace pcutils {
    PointCloud* downsample_pc_random(PointCloud* pc, unsigned int max_points, bool create_new_pc) {
        if(pc->n_points <= max_points) {
            return pc;
        }
        float step = static_cast<float>(pc->n_points) / static_cast<float>(max_points);
        Vertex* new_coords = new Vertex[max_points];
        Color* new_colors = new Color[max_points];
        std::mt19937_64 rng(std::random_device{}());
        std::vector<size_t> idx(pc->n_points);
        std::iota(idx.begin(), idx.end(), 0);

        for (std::size_t i = 0; i < max_points; ++i) {
            std::uniform_int_distribution<std::size_t> dist(i, pc->n_points - 1);
            std::size_t j = dist(rng);
            std::swap(idx[i], idx[j]);
        }

        for (std::size_t i = 0; i < max_points; ++i) {
            new_coords[i] = pc->coords[idx[i]];
            new_colors[i] = pc->colors[idx[i]];
        }
        if(create_new_pc) {
            return new PointCloud{
                pc->timestamp,
                pc->capturer_id,
                pc->frame_nr,
                max_points,
                new_coords,
                new_colors,
                nullptr,
                true,
            };
        }
        delete[] pc->coords;
        delete[] pc->colors;
        pc->coords = new_coords;
        pc->colors = new_colors;
        pc->n_points = max_points;
        return pc;
    }

    bool save_pc_to_ply(const char* file_path, PointCloud* pc) {
        const plywoot::PlyProperty x{"x", plywoot::PlyDataType::Float};
        const plywoot::PlyProperty y{"y", plywoot::PlyDataType::Float};
        const plywoot::PlyProperty z{"z", plywoot::PlyDataType::Float};
        const plywoot::PlyProperty red{"red", plywoot::PlyDataType::UChar};
        const plywoot::PlyProperty green{"green", plywoot::PlyDataType::UChar};
        const plywoot::PlyProperty blue{"blue", plywoot::PlyDataType::UChar};
        const plywoot::PlyElement point{"vertex", pc->n_points, {x, y, z, red, green, blue}};
        using PointLayout = plywoot::reflect::Layout<plywoot::reflect::Pack<float, 3>, plywoot::reflect::Pack<uint8_t, 3>>;
        plywoot::OStream ply_os{plywoot::PlyFormat::BinaryLittleEndian};
        std::vector<PlyPoint> points(pc->n_points);
        for(unsigned int i = 0; i < pc->n_points; i++) {
            points[i].vertex = pc->coords[i];
            points[i].color = pc->colors[i];
        }
        ply_os.add(point, PointLayout{points});
        std::ofstream ofs{file_path, std::ios::out | std::ios::trunc | std::ios::binary};
        ply_os.write(ofs);
        return true;
    }
}
