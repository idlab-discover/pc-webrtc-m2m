#pragma once
#include <vector>
#include <cstdint>
#include <cstring>

class TrackFrame
{
public:
	unsigned int frame_number;
	TrackFrame(unsigned int frame_number, unsigned int frame_length)
		: frame_length(frame_length), frame_number(frame_number), current_size(0), data(frame_length)
	{
	}
	bool insert(char* b, unsigned int frameoffset, unsigned int len, size_t tt)
	{
		if (len == 0)
		{
			return true;
		}
		std::memcpy(&data[frameoffset], b, len);
		current_size += len;
		return true;
	}
	bool is_complete()
	{
		return current_size >= frame_length;
	}
private:
	unsigned int frame_length;
	unsigned int current_size;
	std::vector<char> data;
};

