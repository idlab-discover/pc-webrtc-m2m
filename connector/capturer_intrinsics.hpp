#pragma once
#include <stdio.h>
#include <iostream>
struct CapturerIntrinsics {
    unsigned int  width;
    unsigned int  height;
    unsigned int  model;
    float         ppx;
    float         ppy;
    float         fx;
    float         fy;
    float         coeffs[5];

	static constexpr auto size() {
		return 12*4;
	}

	CapturerIntrinsics(char** buf, size_t& avail) {
		std::memcpy(data(), *buf, size());
		*buf += size();
		avail -= size();
	}

	char* data() {
		return reinterpret_cast<char*>(this);
	}
};