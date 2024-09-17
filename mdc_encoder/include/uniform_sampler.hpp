#include <vector>
#include <string>
#include "point_cloud.h"
#include "description.h"
class UniformSampler {
    private:
        std::vector<float> layer_ratios;
    public:
        UniformSampler(std::vector<float> layer_ratios) : layer_ratios(layer_ratios) {number_of_layers=layer_ratios.size();};   
        std::vector<Description*> create_descriptions(PointCloud* pc);
        int number_of_layers;
};