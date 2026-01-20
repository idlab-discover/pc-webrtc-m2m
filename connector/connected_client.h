#pragma once
#include <map>
#include <vector>
#include <cstdint>
#include "track_internal.h"

class WebRTCConnection;
class ConnectedClient
{
public:
    ConnectedClient(WebRTCConnection* parent, uint32_t client_id);
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
    TrackInternal* add_track(const std::string& track_id, bool is_video);
    TrackInternal** add_tracks(char* const* track_ids, uint8_t* is_video, size_t count); // Maybe change this to normal strings?
private:
	WebRTCConnection* parent;
    uint32_t client_id;
    std::map<uint32_t, TrackInternal> tracks;
};

