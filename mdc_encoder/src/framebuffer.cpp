#include "framebuffer.hpp"

void FrameBuffer::add_to_buffer(Frame* frame)
{
    std::unique_lock lock(m);
    while(frames.size() >= max_size) {
        delete frames.front();
        frames.pop();
    }
    frames.push(frame);
    lock.unlock();
    cv.notify_one();
}

Frame* FrameBuffer::get_next_frame()
{
    std::unique_lock lock(m);
    return internal_get_next_frame();
}

Frame* FrameBuffer::poll_next_frame()
{
    std::unique_lock lk(m);
    cv.wait(lk, [this]{ return get_buffer_size() > 0 || is_stopped; });
    return internal_get_next_frame();
}

size_t FrameBuffer::get_buffer_size()
{
    return frames.size();
}

void FrameBuffer::clear_buffer()
{
    std::unique_lock lk(m);
    while (!frames.empty()) {
        delete frames.front();
        frames.pop();
    }
    
}

void FrameBuffer::stop_buffer() {
    std::unique_lock lk(m);
    is_stopped = true;
    lk.unlock();
    cv.notify_all();
}

Frame* FrameBuffer::internal_get_next_frame()
{
    if(is_stopped || frames.empty()) return nullptr;
    Frame* frame = frames.front();
    frames.pop();
    return frame;
}
