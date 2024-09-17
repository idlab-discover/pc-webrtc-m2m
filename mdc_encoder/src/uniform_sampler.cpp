#include "uniform_sampler.hpp"
#include <algorithm>
#include <numeric>
#include <random>
std::vector<Description*> UniformSampler::create_descriptions(PointCloud* pc) {
    std::vector<Description*> descs;
    std::vector<int> pc_points(pc->n_points) ;
    std::iota (std::begin(pc_points), std::end(pc_points), 0); // Fill with 0, 1, ..., size -1.
    std::shuffle(pc_points.begin(), pc_points.end(), std::mt19937{std::random_device{}()});
    std::vector<unsigned int> end_border;
    unsigned int total_points_used = 0;
    for(int i = 0; i < number_of_layers; i++) {
        Description* desc = new Description();
        desc->frame_nr = pc->frame_nr;
        desc->description_nr = i;
        desc->n_points_in_total = pc->n_points;
        if(i < number_of_layers - 1) {
            desc->n_points = pc->n_points*layer_ratios[i];
        } else {
            desc->n_points = pc->n_points - total_points_used; // Add rounding error points to this description
        }
        desc->colors = new Color[desc->n_points];
        desc->coords = new Vertex[desc->n_points];
        descs.push_back(desc);
        end_border.push_back(desc->n_points+total_points_used);
        total_points_used += desc->n_points;
    }

    for(int i=0; i < pc_points.size(); i++) {
        if(pc_points[i] >= 0 && pc_points[i] < end_border[0]) {
            descs[0]->coords[descs[0]->current_points] = pc->coords[i];
            descs[0]->colors[descs[0]->current_points] = pc->colors[i];
            descs[0]->current_points++;       
        } else if (pc_points[i] >= end_border[0] && pc_points[i] < end_border[1]) {
            descs[1]->coords[descs[1]->current_points] = pc->coords[i];
            descs[1]->colors[descs[1]->current_points] = pc->colors[i];
            descs[1]->current_points++;
          
        } else {
            descs[2]->coords[descs[2]->current_points] = pc->coords[i];
            descs[2]->colors[descs[2]->current_points] = pc->colors[i];
            descs[2]->current_points++;
        }
    }
    return descs;
}