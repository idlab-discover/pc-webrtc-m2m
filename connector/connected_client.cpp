#include "pch.h"
#include "connected_client.h"
#include "webrtc_connection.h"
ConnectedClient::ConnectedClient(WebRTCConnection* parent, uint32_t client_id)
    : parent(parent), client_id(client_id)
{
    
}

TrackInternal* ConnectedClient::add_track(const std::string& track_id, bool is_video)
{
    // TODO Perform some checks if track does or does not exist
    unsigned int internal_id = parent->add_track(track_id, is_video);
    auto emplace_result = tracks.emplace(
        std::piecewise_construct,
        std::forward_as_tuple(internal_id),
        std::forward_as_tuple(10, 10)
    );
    return get_track_ptr(internal_id);
}

TrackInternal** ConnectedClient::add_tracks(char* const* track_ids, uint8_t* is_video, size_t count)
{
	std::vector<unsigned int> internal_ids = parent->add_tracks(client_id, track_ids, is_video, count);
	TrackInternal** track_ptrs = new TrackInternal*[count];
    for(size_t i = 0; i < count; i++) {
        tracks.emplace(
            std::piecewise_construct,
            std::forward_as_tuple(internal_ids[i]),
            std::forward_as_tuple(10, 10)
        );
		track_ptrs[i] = get_track_ptr(internal_ids[i]);
	}

    return track_ptrs;
}
