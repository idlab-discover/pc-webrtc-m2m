#include "ply_frame.hpp"
#include "external/plywoot/plywoot.hpp"
#include <fstream>
void PlyFrame::make_data_arrays(const std::string& file_path)
{
    using PointLayout = plywoot::reflect::Layout<plywoot::reflect::Pack<float, 3>, plywoot::reflect::Pack<uint8_t, 3>>;
    std::ifstream file_stream(file_path, std::ios::binary);
    if (!file_stream.is_open()) {
        throw std::runtime_error("Failed to open file: " + file_path);
    }
    // TODO Probably optimize this
    const plywoot::IStream ply_is{file_stream};
    const std::vector<PlyPointRead> temp_vector = ply_is.readElement<PlyPointRead, PointLayout>();
    n_points = static_cast<unsigned int>(temp_vector.size());
    vertices.resize(n_points);
    colors.resize(n_points);
    for(unsigned int i = 0; i < n_points; i++) {
        vertices[i] = temp_vector[i].vertex;
        colors[i] = temp_vector[i].color;
    }
    
}