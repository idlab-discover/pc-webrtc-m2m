#include "pch.h"
#include "track_internal.h"

TrackInternal::TrackInternal(size_t max_priority_queue_size, uint32_t incomplete_queue_max_size)
    : max_priority_queue_size(max_priority_queue_size), incomplete_frame_queue(incomplete_queue_max_size)
{
}

void TrackInternal::add_frame_to_priority_queue(TrackFrame* frame)
{
    std::unique_lock<std::mutex> lock(mtx);
	incomplete_frame_queue.remove_frame(frame->frame_number);
    // If full, remove the oldest (lowest frame_number)
    if (priority_frame_queue.size() >= max_priority_queue_size) {
        auto it = priority_frame_queue.begin();
        if (it != priority_frame_queue.end()) {
            delete *it; // If ownership, otherwise just erase
            priority_frame_queue.erase(it);
        }
    }
    priority_frame_queue.insert(frame);
    cv.notify_all();
}

TrackFrame* TrackInternal::pop_oldest_frame()
{
    std::unique_lock<std::mutex> lock(mtx);
    if (priority_frame_queue.empty()) return nullptr;
    auto it = priority_frame_queue.begin();
    TrackFrame* oldest = *it;
    priority_frame_queue.erase(it);
    return oldest;
}

TrackFrame* TrackInternal::wait_and_pop_oldest_or_null()
{
    std::unique_lock<std::mutex> lock(mtx);
    cv.wait(lock, [this] { return !priority_frame_queue.empty() || stopped; });
    if (!priority_frame_queue.empty()) {
        auto it = priority_frame_queue.begin();
        TrackFrame* oldest = *it;
        priority_frame_queue.erase(it);
        return oldest;
    }
    // If stopped and queue is empty
    return nullptr;
}

void TrackInternal::stop_track()
{
    std::unique_lock<std::mutex> lock(mtx);
    stopped = true;
    cv.notify_all();
}
