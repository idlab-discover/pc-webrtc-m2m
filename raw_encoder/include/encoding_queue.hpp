#pragma once
#include <vector>
#include <mutex>
#include <queue>
#include <map>
#include "threadpool.h"
#include "framework.h"
#include "raw_encoder.hpp"
#include "log.h"
class EncodingQueue;
extern "C" {
    typedef void(*ColorDoneCallback)(unsigned char* raw_data_ptr, uint32_t size, uint32_t capturer_id, uint32_t frame_nr, uint32_t width, uint32_t height, uint32_t n_points,  uint64_t timestamp);
   
    typedef void(*DepthDoneCallback)(unsigned char* raw_data_ptr, uint32_t size, uint32_t capturer_id, uint32_t frame_nr, uint32_t width, uint32_t height, uint32_t n_points, uint64_t timestamp);
   
    typedef void(*FreeFrameCallback)(RawFrame* pc);
   

    DLLExport void register_color_done_callback(EncodingQueue* enc_queue, ColorDoneCallback cb);
    DLLExport void register_depth_done_callback(EncodingQueue* enc_queue, DepthDoneCallback cb);
	DLLExport void register_free_frame_callback(EncodingQueue* enc_queue, FreeFrameCallback cb);
}


class EncodingQueue {
    public:
        RawEncoder enc;
        EncodingQueue
        (
            unsigned int max_queue, unsigned int n_workers, unsigned int width, unsigned int height, unsigned int n_capturers,
            ColorCodecType col_codec, void* col_codec_settings,
            DepthCodecType dep_codec, void* dep_codec_settings
        ) : max_queue(max_queue), n_capturers(n_capturers), enc(width, height, col_codec, col_codec_settings, dep_codec, dep_codec_settings) 
        {
            Log::log_to_file(std::format("id={} ts={} status={}", NAME, Log::get_time(), (int)Log::Status::Creating), false);
            pool.start(n_workers);
            Log::log_to_file(std::format("id={} ts={} status={}", NAME, Log::get_time(), (int)Log::Status::Created), false);
        };
        ~EncodingQueue() {
            Log::log_to_file(std::format("id={} ts={} status={}", NAME, Log::get_time(), (int)Log::Status::Destroying), false);
            pool.stop();
            Log::log_to_file(std::format("id={} ts={} status={}", NAME, Log::get_time(), (int)Log::Status::Destroyed), false);
        }
        // TODO stop threads
        int enqueue_frame(RawFrame* pc);
        
        bool is_ready() {return enc.is_ready();};

        // ###### Callbacks #########
        void register_color_done_callback(ColorDoneCallback cb) {
            color_done_callback_instance = cb;
        }
        void register_depth_done_callback(DepthDoneCallback cb) {
            depth_done_callback_instance = cb;
        }
        void register_free_frame_callback(FreeFrameCallback cb) {
            free_frame_callback_instance = cb;
        }
        
    private:
        const std::string NAME = "EncodingQueue";
        ColorDoneCallback color_done_callback_instance = nullptr;
        DepthDoneCallback depth_done_callback_instance = nullptr;
        FreeFrameCallback free_frame_callback_instance = nullptr;
        unsigned int latest_entered_frame_nr = 0;
        unsigned int latest_dropped_frame_nr = 0;
        unsigned int n_capturers;
        std::mutex m_enqueue;
        std::condition_variable cv_enqueue;
        RawFrame* current_in_wait = nullptr;
        std::queue<bool> q_enqueued;
        unsigned int max_queue;
        std::map<unsigned int, std::map<unsigned int, unsigned int>> coding_status;
        void complete_encoding(EncodedRaw* dsc);
        ThreadPool pool;
        void internal_enqueue_frame(RawFrame* pc);
        
        
        
};

// Jobs vs threads
