#pragma once
#include <vector>
#include <mutex>
#include <queue>
#include <map>
#include "threadpool.h"
#include "framework.h"
#include "raw_encoder.hpp"
extern "C" {
    typedef void(*ColorDoneCallback)(unsigned char* raw_data_ptr, uint32_t size, uint32_t frame_nr, uint32_t width, uint32_t height, uint32_t n_points,  uint64_t timestamp);
    static ColorDoneCallback color_done_callback_instance = nullptr;
    typedef void(*DepthDoneCallback)(unsigned char* raw_data_ptr, uint32_t size, uint32_t frame_nr, uint32_t width, uint32_t height, uint32_t n_points, uint64_t timestamp);
    static DepthDoneCallback depth_done_callback_instance = nullptr;
    typedef void(*FreeFrameCallback)(RawFrame* pc);
    static FreeFrameCallback free_frame_callback_instance = nullptr;

    DLLExport void register_color_done_callback(ColorDoneCallback cb);
    DLLExport void register_depth_done_callback(DepthDoneCallback cb);
	DLLExport void register_free_frame_callback(FreeFrameCallback cb);
}


class EncodingQueue {
    public:
        RawEncoder enc;
        EncodingQueue(unsigned int max_queue, unsigned int width, unsigned int height, unsigned int jpeg_quality) : max_queue(max_queue), enc(width, height, jpeg_quality) {
            pool.start(3);
        };
        ~EncodingQueue() {
            pool.stop();
        }
        // TODO stop threads
        int enqueue_frame(RawFrame* pc);
        
        // ###### Callbacks #########
        
        
    private:
        std::mutex m_enqueue;
        std::condition_variable cv_enqueue;
        RawFrame* current_in_wait = nullptr;
        std::queue<bool> q_enqueued;
        unsigned int max_queue;
        std::map<unsigned int, unsigned int> coding_status;
        void complete_encoding(EncodedRaw* dsc);
        ThreadPool pool;
        void internal_enqueue_frame(RawFrame* pc);
        
        
        
};

// Jobs vs threads
