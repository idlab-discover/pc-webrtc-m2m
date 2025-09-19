#pragma once
#include "track_frame_queue.h"
#include <set>
#include <functional>
#include <mutex>
#include <condition_variable>

class TrackInternal
{
private:
    TrackFrameQueue incomplete_frame_queue;
    // Comparator for TrackFrame pointers by frame_number
    struct TrackFrameComparator {
        bool operator()(const TrackFrame* a, const TrackFrame* b) const {
            return a->frame_number < b->frame_number;
        }
    };
    std::set<TrackFrame*, TrackFrameComparator> priority_frame_queue;
    size_t max_priority_queue_size;
    bool stopped = false;
    std::mutex mtx;
    std::condition_variable cv;
public:
    // Add a TrackFrame to the priority queue, evicting the oldest if full
    void add_frame_to_priority_queue(TrackFrame* frame);
    // Optionally, a method to get and remove the oldest frame
    TrackFrame* pop_oldest_frame();
    // Wait and pop oldest frame, or return nullptr if stopped
    TrackFrame* wait_and_pop_oldest_or_null();
    // Constructor to set max size for both queues
    TrackInternal(size_t max_priority_queue_size, uint32_t incomplete_queue_max_size);
    TrackFrame* get_incomplete_frame(uint32_t frame_number, uint32_t frame_length) {
        return incomplete_frame_queue.get_frame(frame_number, frame_length);
	}
	// Remove an incomplete frame
    void remove_incomplete_frame(uint32_t frame_number) {
        incomplete_frame_queue.remove_frame(frame_number);
	}
    // Stop the track
    void stop_track();
};

