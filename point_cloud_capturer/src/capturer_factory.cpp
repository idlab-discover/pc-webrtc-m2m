#include "capturer_factory.hpp"
#include "artificical_capturer.hpp"
#include "rs2_capturer.hpp"
#include "prerecorded_kinect_capturer.hpp"
#include "ply_capturer.hpp"
#include "log.h"
CapturerFactory& CapturerFactory::get_instance() {
    static CapturerFactory instance;
    return instance;
}
CapturerFactory::~CapturerFactory() {
    // Destructor implementation (if needed)
}
Capturer* CapturerFactory::create_capturer(unsigned int capture_id, uint32_t fps, FrameMode mode, FrameCleanupSettings cleanup_settings, CAPTURE_TYPE type, void* capture_settings) {
    try {
        switch (type) {
            case CAPTURE_TYPE::Artifical: {
                Log::custom_log("create_capturer: Creating artificial capturer", LOG_LEVEL::Default, LogColor::Orange);
                return new ArtificalCapturer(capture_id, fps, mode, cleanup_settings, static_cast<ArtificalCaptureSettings*>(capture_settings));
            }
            #ifdef USE_REALSENSE
            case CAPTURE_TYPE::RealSense: {
                Log::custom_log("create_capturer: Creating realsense2 capturer", LOG_LEVEL::Default, LogColor::Orange);
                return new RS2Capturer(capture_id, fps, mode, cleanup_settings, static_cast<RS2CaptureSettings*>(capture_settings));
            }
            #endif
            #ifdef USE_KINECT
            case CAPTURE_TYPE::PrerecordedKinect: {
                Log::custom_log("create_capturer: Creating prerecorded kinect capturer", LOG_LEVEL::Default, LogColor::Orange);
                return new PrerecordedKinectCapturer(capture_id, fps, mode, cleanup_settings, static_cast<PrerecordedKinectCaptureSettings*>(capture_settings));
            }
            #endif
            case CAPTURE_TYPE::PlyFiles: {
                Log::custom_log("create_capturer: Creating ply file capturer", LOG_LEVEL::Default, LogColor::Orange);
                return new PlyCapturer(capture_id, fps, mode, cleanup_settings, static_cast<PlyCaptureSettings*>(capture_settings));
            }
            default:
                Log::custom_log("create_capturer: Unknown capture type", LOG_LEVEL::Default, LogColor::Red);
                return nullptr;
        }
   
	} catch (CAPTURER_SETUP_CODE e) {
		return nullptr;
	}
}

MultiCapturer* CapturerFactory::create_multi_capturer(uint32_t fps, FrameMode mode, FrameCleanupSettings cleanup_settings, CAPTURE_TYPE type, unsigned int n_settings, void** capture_settings) {
    std::vector<Capturer*> capturers;
    for(int i = 0; i < n_settings; i++) {
        Capturer* capturer = create_capturer(i, fps, mode, cleanup_settings, type, capture_settings[i]);
        if (capturer != nullptr) {
            capturers.push_back(capturer);
        } else {
            Log::custom_log("create_multi_capturer: Failed to create capturer", LOG_LEVEL::Default, LogColor::Red);
            return nullptr;
        }
    }
    return new MultiCapturer(fps, mode, cleanup_settings, type, std::move(capturers));
}