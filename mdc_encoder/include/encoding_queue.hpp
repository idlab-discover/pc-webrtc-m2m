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
extern "C" {
    typedef void(*DescriptionDoneCallback)(Description* dsc, char* raw_data_ptr, uint32_t n_points_in_total, uint32_t dsc_size, uint32_t frame_nr, uint32_t dsc_nr);
    static DescriptionDoneCallback description_done_callback_instance = nullptr;
    typedef void(*FreePointCloudCallback)(PointCloud* pc);
    static FreePointCloudCallback free_pc_callback_instance = nullptr;

    DLLExport void register_description_done_callback(DescriptionDoneCallback cb);
	DLLExport void register_free_pc_callback(FreePointCloudCallback cb);
}


class EncodingQueue {
    public:
        EncodingQueue(unsigned int max_queue) : max_queue(max_queue) {
            pool.start(3);
        };
        ~EncodingQueue() {
            pool.stop();
        }
        // TODO stop threads
        int enqueue_pc(PointCloud* pc);
        
        // ###### Callbacks #########
        
        
    private:
        std::mutex m_enqueue;
        std::condition_variable cv_enqueue;
        PointCloud* current_in_wait = nullptr;
        std::queue<bool> q_enqueued;
        unsigned int max_queue;
        std::map<unsigned int, unsigned int> coding_status;
        void complete_encoding(Description* dsc);
        ThreadPool pool;
        void internal_enqueue_pc(PointCloud* pc);

        
        
};

// Jobs vs threads
