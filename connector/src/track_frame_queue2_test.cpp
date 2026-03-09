#include "pch.h"
#include "track_frame_queue.h"
#include "track_internal.h"
#include <cassert>
#include <iostream>
#include <thread>

void test_basic_insert_and_retrieve() {
    TrackFrameQueue queue(3);
    auto* f1 = queue.get_frame(1, 10);
    assert(f1 != nullptr);
    auto* f2 = queue.get_frame(2, 10);
    assert(f2 != nullptr);
    auto* f3 = queue.get_frame(3, 10);
    assert(f3 != nullptr);
    // Should retrieve same pointer
    assert(queue.get_frame(1, 10) == f1);
    assert(queue.get_frame(2, 10) == f2);
    assert(queue.get_frame(3, 10) == f3);
}

void test_eviction() {
    TrackFrameQueue queue(3);
    queue.get_frame(1, 10);
    queue.get_frame(2, 10);
    queue.get_frame(3, 10);
    // This should evict frame 1
    auto* f4 = queue.get_frame(4, 10);
    assert(f4 != nullptr);
    assert(queue.get_frame(1, 10) == nullptr);
    assert(queue.get_frame(2, 10) != nullptr);
    assert(queue.get_frame(3, 10) != nullptr);
    assert(queue.get_frame(4, 10) == f4);
}

void test_remove_frame() {
    TrackFrameQueue queue(3);
    queue.get_frame(1, 10);
    queue.get_frame(2, 10);
    queue.get_frame(3, 10);
    queue.remove_frame(2);
    assert(queue.get_frame(2, 10) != nullptr); // Should create new frame
    queue.remove_frame(1);
    assert(queue.get_frame(1, 10) != nullptr); // Should create new frame
}

void test_past_frame() {
    TrackFrameQueue queue(3);
    queue.get_frame(1, 10);
    queue.get_frame(2, 10);
    queue.get_frame(3, 10);
    queue.get_frame(4, 10); // Evicts frame 1
    assert(queue.get_frame(1, 10) == nullptr);
}

void test_priority_queue_basic() {
    TrackInternal track(3, 3);
    auto* f1 = new TrackFrame(1, 10);
    auto* f2 = new TrackFrame(2, 10);
    auto* f3 = new TrackFrame(3, 10);
    track.add_frame_to_priority_queue(f2);
    track.add_frame_to_priority_queue(f3);
    track.add_frame_to_priority_queue(f1);
    // Should pop the lowest frame number (1)
    TrackFrame* oldest = track.pop_oldest_frame();
    assert(oldest->frame_number == 1);
    delete oldest;
}

void test_priority_queue_eviction() {
    TrackInternal track(2, 3);
    auto* f1 = new TrackFrame(1, 10);
    auto* f2 = new TrackFrame(2, 10);
    auto* f3 = new TrackFrame(3, 10);
    track.add_frame_to_priority_queue(f1);
    track.add_frame_to_priority_queue(f2);
    track.add_frame_to_priority_queue(f3); // Should evict f1
    TrackFrame* oldest = track.pop_oldest_frame();
    assert(oldest->frame_number == 2);
    delete oldest;
    oldest = track.pop_oldest_frame();
    assert(oldest->frame_number == 3);
    delete oldest;
}

void test_wait_and_pop_oldest_returns_frame() {
    TrackInternal track(3, 3);
    TrackFrame* result = nullptr;
    std::thread t([&] {
        result = track.wait_and_pop_oldest_or_null();
    });
    std::this_thread::sleep_for(std::chrono::milliseconds(100)); // Ensure thread is waiting
    auto* f1 = new TrackFrame(1, 10);
    track.add_frame_to_priority_queue(f1);
    t.join();
    assert(result == f1);
    delete result;
}

void test_wait_and_pop_oldest_returns_null_on_stop() {
    TrackInternal track(3, 3);
    TrackFrame* result = nullptr;
    std::thread t([&] {
        result = track.wait_and_pop_oldest_or_null();
    });
    std::this_thread::sleep_for(std::chrono::milliseconds(100)); // Ensure thread is waiting
    track.stop_track();
    t.join();
    assert(result == nullptr);
}

int main() {
    test_basic_insert_and_retrieve();
    test_eviction();
    test_remove_frame();
    test_past_frame();
    test_priority_queue_basic();
    test_priority_queue_eviction();
    test_wait_and_pop_oldest_returns_frame();
    test_wait_and_pop_oldest_returns_null_on_stop();
    std::cout << "All TrackFrameQueue tests passed!\n";
    std::cout << "All TrackInternal priority queue tests passed!\n";
    std::cout << "All TrackInternal wait_and_pop_oldest_or_null tests passed!\n";
    return 0;
}
