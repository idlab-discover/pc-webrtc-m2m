#pragma once
#include <vector>
#include <mutex>
#include <queue>
#include <map>
#include <draco/point_cloud/point_cloud.h>
#include <draco/point_cloud/point_cloud_builder.h>
#include <draco/compression/encode.h>
#include "point_cloud.h"
#include "description.h"
#include "threadpool.h"
#include "framework.h"
#include "uniform_sampler.hpp"

class EncodingQueue;
extern "C" {
    typedef void(*DescriptionDoneCallback)(Description* dsc, char* raw_data_ptr, uint32_t n_points_in_total, uint32_t dsc_size, uint32_t capturer_id, uint32_t frame_nr, uint32_t dsc_nr, uint64_t timestamp);
    typedef void(*FreePointCloudCallback)(PointCloud* pc);

    DLLExport void register_description_done_callback(EncodingQueue* enc_queue, DescriptionDoneCallback cb);
	DLLExport void register_free_pc_callback(EncodingQueue* enc_queue, FreePointCloudCallback cb);
}


class EncodingQueue {
    public:
        EncodingQueue(unsigned int max_queue, float* layer_ratios, unsigned int number_of_layers) 
        : max_queue(max_queue), us(std::vector<float>(layer_ratios, layer_ratios+number_of_layers)) {
            pool.start(3);
        };
        ~EncodingQueue() {
           
        };
        void stop() {
            pool.stop();
        };
        // TODO stop threads
        int enqueue_pc(PointCloud* pc);
        
        // ###### Callbacks #########
         void register_description_done_callback(DescriptionDoneCallback cb) {
            description_done_callback_instance = cb;
        }
        void register_free_pc_callback(FreePointCloudCallback cb) {
            free_pc_callback_instance = cb;
        }
        std::vector<Description*> create_descriptions(PointCloud* pc, float multi) {
            return us.create_descriptions(pc, multi);
        }
    private:
        std::mutex m_enqueue;
        std::condition_variable cv_enqueue;
        PointCloud* current_in_wait = nullptr;
        std::queue<bool> q_enqueued;
        unsigned int max_queue;
        UniformSampler us;
        DescriptionDoneCallback description_done_callback_instance = nullptr;
        FreePointCloudCallback free_pc_callback_instance = nullptr;
        std::map<unsigned int, unsigned int> coding_status;
        void complete_encoding(Description* dsc);
        ThreadPool pool;
        void internal_enqueue_pc(PointCloud* pc);

        
        
};

// Jobs vs threads
