#pragma once
#include <vector>
#include <queue>
#include "frame.hpp"
#include <mutex>
#include <condition_variable>
class FrameBuffer {
    public:
        FrameBuffer(unsigned int max_size = 5) : max_size(max_size) {};
        void set_max_size(unsigned int _max_size) { std::unique_lock lk(m); max_size=_max_size; };
        void add_to_buffer(Frame* rs2_frame);
        Frame* get_next_frame();
        Frame* poll_next_frame();
        size_t get_buffer_size();
        void clear_buffer();
        void stop_buffer();
    private:
        std::queue<Frame*> frames;
        std::mutex m;
        std::condition_variable cv;
        unsigned int max_size;
        bool is_stopped = false;
        Frame* internal_get_next_frame();
};