#include "pch.h"
#include "connected_client.h"

ConnectedClient::ConnectedClient(uint32_t client_id, const std::vector<uint32_t>& track_ids)
    : client_id(client_id), tracks{}
{
    for (auto id : track_ids) {
        tracks.emplace(std::piecewise_construct,
                        std::forward_as_tuple(id),
                        std::forward_as_tuple(10, 10));
    }
}
