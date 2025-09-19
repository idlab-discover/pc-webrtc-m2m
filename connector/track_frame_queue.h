#pragma once
#include "track_frame.h"
#include <vector>
#include <cstdint>

class TrackFrameQueue {
public:
    TrackFrameQueue(uint32_t max_size)
        : max_size(max_size), frames(max_size, nullptr), start_frame_number(0), start_index(0) {}

    ~TrackFrameQueue() {
        for (auto* f : frames) {
            delete f;
        }
    }

    TrackFrame* get_frame(uint32_t frame_number, uint32_t frame_length) {
        if (frame_number < start_frame_number) {
            return nullptr;
        }
        uint32_t distance = frame_number - start_frame_number;
        if (distance >= max_size) {
            // Only evict frames that will be replaced
            uint32_t frames_to_replace = distance - max_size + 1;
            for (uint32_t i = 0; i < frames_to_replace; ++i) {
                uint32_t idx = (start_index + i) % max_size;
                delete frames[idx];
                frames[idx] = nullptr;
            }
            start_frame_number = frame_number - max_size + 1;
            start_index = (start_index + frames_to_replace) % max_size;
            distance = frame_number - start_frame_number;
        }
        uint32_t idx = (start_index + distance) % max_size;
        if (frames[idx] == nullptr) {
            // If overwriting, cleanup
            delete frames[idx];
            frames[idx] = new TrackFrame(frame_number, frame_length);
        }
        return frames[idx];
    }

    void remove_frame(uint32_t frame_number) {
        if (frame_number < start_frame_number) {
            return;
        }
        uint32_t distance = frame_number - start_frame_number;
        if (distance >= max_size) {
            return;
        }
        uint32_t idx = (start_index + distance) % max_size;
        if (frames[idx] != nullptr) {
            frames[idx] = nullptr;
        }
    }

private:
    std::vector<TrackFrame*> frames;
    uint32_t max_size;
    uint32_t start_frame_number;
    uint32_t start_index;
};

