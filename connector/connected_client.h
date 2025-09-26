#pragma once
#include <map>
#include <vector>
#include <cstdint>
#include "track_internal.h"

class ConnectedClient
{
public:
    ConnectedClient(uint32_t client_id, const std::vector<uint32_t>& track_ids);
    ConnectedClient(const ConnectedClient&) = delete;
    ConnectedClient& operator=(const ConnectedClient&) = delete;
    uint32_t get_client_id() const { return client_id; }
    std::map<uint32_t, TrackInternal>& get_tracks() { return tracks; }
    // Returns a reference to the requested track by id. Throws std::out_of_range if not found.
    TrackInternal& get_track(uint32_t track_id) {
        return tracks.at(track_id);
    }
    TrackInternal* get_track_ptr(uint32_t track_id) {
        return &tracks.at(track_id);
    }
private:
    uint32_t client_id;
    std::map<uint32_t, TrackInternal> tracks;
};

