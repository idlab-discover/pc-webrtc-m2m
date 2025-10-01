#include "pch.h"
#include "connected_client.h"
#include "webrtc_connection.h"

ConnectedClient::ConnectedClient(WebRTCConnection* parent, uint32_t client_id)
    : parent(parent), client_id(client_id)
{
    
}

TrackInternal* ConnectedClient::add_track(const std::string& track_id)
{
    // TODO Perform some checks if track does or does not exist
    unsigned int internal_id = parent->add_track(track_id);
    auto emplace_result = tracks.emplace(
        std::piecewise_construct,
        std::forward_as_tuple(internal_id),
        std::forward_as_tuple(10, 10)
    );
    return get_track_ptr(internal_id);
}