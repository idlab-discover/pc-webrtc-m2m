#include <string>
#include "log.h"

#define LOG_COMMON_FORMAT "id={} ts={} status={}"
#define LOG_COMMON_FORMAT_ENC LOG_COMMON_FORMAT " capturerID={} frameNr={}"
#define LOG_COMMON_ARGS(NAME, STATUS) NAME, Log::get_time(), (int)STATUS

#ifndef ENABLE_LOGGING

#define LOG_TO_FILE_FMT(FMT, ...) \
    Log::log_to_file(std::format(FMT, __VA_ARGS__), false);

#define LOG_STATUS_TO_FILE(NAME, STATUS) \
    LOG_TO_FILE_FMT(LOG_COMMON_FORMAT, LOG_COMMON_ARGS(NAME, STATUS))

#define LOG_FRAME_STATUS_TO_FILE(NAME, STATUS, CAPTURER_ID, FRAME_NR) \
    LOG_TO_FILE_FMT(LOG_COMMON_FORMAT_ENC, LOG_COMMON_ARGS(NAME, STATUS), CAPTURER_ID, FRAME_NR)

#define LOG_FRAME_STATUS_TO_FILE_LIMITED(NAME, STATUS, CAPTURER_ID, FRAME_NR) \
    if(!Log::is_logging_limited() || (FRAME_NR % Log::get_every_n_frames() == 0)) { \
        LOG_FRAME_STATUS_TO_FILE(NAME, STATUS, CAPTURER_ID, FRAME_NR); \
    }

#define LOG_FRAME_STATUS_SIZE_TO_FILE_LIMITED(NAME, STATUS, CAPTURER_ID, FRAME_NR, SIZE) \
    if(!Log::is_logging_limited() || (FRAME_NR % Log::get_every_n_frames() == 0)) { \
        LOG_TO_FILE_FMT(LOG_COMMON_FORMAT_ENC " size={}", LOG_COMMON_ARGS(NAME, STATUS), CAPTURER_ID, FRAME_NR, SIZE); \
    }



#else

#endif