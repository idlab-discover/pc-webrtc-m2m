#include "artificical_frame.hpp"
#include "point_cloud.hpp"
void HSVtoRGB(float H, float S, float V, float& R, float& G, float& B) {
    float C = V * S;
    float X = C * (1 - std::fabs(std::fmod(H / 60.0, 2) - 1));
    float m = V - C;

    if (H >= 0 && H < 60) {
        R = C; G = X; B = 0;
    }
    else if (H >= 60 && H < 120) {
        R = X; G = C; B = 0;
    }
    else if (H >= 120 && H < 180) {
        R = 0; G = C; B = X;
    }
    else if (H >= 180 && H < 240) {
        R = 0; G = X; B = C;
    }
    else if (H >= 240 && H < 300) {
        R = X; G = 0; B = C;
    }
    else {
        R = C; G = 0; B = X;
    }

    R += m; G += m; B += m;
}

void ArtificalFrame::make_data_arrays(unsigned int side_size)
{
    n_points = side_size*side_size*side_size;
    float base_x = 0.1;
    float base_y = 0.1;
    float base_z = 0.1;
    points = new Vertex[n_points];
    colors = new Color[n_points];
    unsigned int current_point = 0;
    float hue = std::fmod(static_cast<float>(std::clock()) / CLOCKS_PER_SEC * 60, 360.0f);
    float r, g, b;
    HSVtoRGB(hue, 1.0f, 1.0f, r, g, b);
    uint8_t r_small = static_cast<uint8_t>(r*255);
    uint8_t g_small = static_cast<uint8_t>(g*255);
    uint8_t b_small = static_cast<uint8_t>(b*255);
    x_offset = base_x*side_size / 2;
    y_offset = base_y*side_size / 2;
    z_offset = base_z*side_size / 2;
    for(unsigned int i = 0; i < side_size; i++) {
        for(unsigned int j = 0; j < side_size; j++) {
            for(unsigned int k = 0; k < side_size; k++) {
                Vertex v{base_x*i, base_y*j, base_z*k};
                Color c{r_small,g_small,b_small};
                points[current_point] = v;
                colors[current_point] = c;
                current_point++;
            }
        }
    }
}


