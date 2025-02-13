#include "rs2_capturer.hpp"
#include "rs2_frame.hpp"
CAPTURER_SETUP_CODE RS2Capturer::init()
{
    rs2::config cfg;
	try {
		//capturer = new RS2Capturer();
		cfg.enable_stream(RS2_STREAM_COLOR, width, height, RS2_FORMAT_RGB8, fps);
		cfg.enable_stream(RS2_STREAM_DEPTH, width, height, RS2_FORMAT_Z16, fps);
		rs2::pipeline_profile selection = pipe.start(cfg);
		rs2::device selected_device = selection.get_device();
		depth_sensor.emplace(selected_device.first<rs2::depth_sensor>());
		color_sensor.emplace(selected_device.first<rs2::color_sensor>());

		thres_filter.set_option(RS2_OPTION_MIN_DISTANCE, min_dist);
		thres_filter.set_option(RS2_OPTION_MAX_DISTANCE, max_dist);

		if (depth_sensor->supports(RS2_OPTION_EMITTER_ENABLED))
		{
			depth_sensor->set_option(RS2_OPTION_EMITTER_ENABLED, 1.f); // Enable emitter
			pipe.wait_for_frames();
			//  depth_sensor.set_option(RS2_OPTION_EMITTER_ENABLED, 0.f); // Disable emitter
		}

		if (depth_sensor->supports(RS2_OPTION_LASER_POWER))
		{
			auto range = depth_sensor->get_option_range(RS2_OPTION_LASER_POWER);
			depth_sensor->set_option(RS2_OPTION_LASER_POWER, range.max); // Set max power
			Sleep(1);
			std::cout << "laser power " << range.max << std::endl;
			//depth_sensor.set_option(RS2_OPTION_LASER_POWER, 0.f); // Disable laser
		}
		
	} catch (...) {
		return exception_handler().first;	
	}
	
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

CAPTURER_SETUP_CODE RS2Capturer::capture_next_frame()
{
    try {
		size_t n_frames = 0;
		rs2::frameset frames;
		while(n_frames != 2) {
			frames = pipe.wait_for_frames();
			n_frames = frames.size();
		}
		frames = depth_align.process(frames);
		auto depth = frames.get_depth_frame();
		depth = thres_filter.process(depth);
		auto rgb = frames.get_color_frame();
	
		frame_buffer.add_to_buffer(new RS2Frame(
			mode,
			width,
			height,
			rgb.get_bytes_per_pixel(),
			rgb.get_stride_in_bytes(),
			depth,
			rgb,
			frame_nr
		));
        
    } catch (...) {
        return exception_handler().first;
    }
	frame_nr++;
    return CAPTURER_SETUP_CODE::StartedCorrectly;
}

Frame *RS2Capturer::poll_next_frame()
{
    return frame_buffer.poll_next_frame();
}

std::pair<CAPTURER_SETUP_CODE, std::string> RS2Capturer::exception_handler() noexcept
{
    try {
        throw;
    } catch (rs2::camera_disconnected_error e) {
		return std::make_pair(CAPTURER_SETUP_CODE::CameraDisconnected, e.what());
		
	} catch (rs2::backend_error e) {
		return std::make_pair(CAPTURER_SETUP_CODE::BackendError, e.what());
		
	} catch (rs2::invalid_value_error e) {
		return std::make_pair(CAPTURER_SETUP_CODE::InvalidValue, e.what());
		
	} catch (rs2::wrong_api_call_sequence_error e) {
		return std::make_pair(CAPTURER_SETUP_CODE::WrongApiCallSeq, e.what());
		
	} catch (rs2::not_implemented_error e) {
		return std::make_pair(CAPTURER_SETUP_CODE::NotImpl, e.what());
		
	} catch (rs2::device_in_recovery_mode_error e) {
		return std::make_pair(CAPTURER_SETUP_CODE::DeviceInRecovery, e.what());
	} catch (...) {
		return std::make_pair(CAPTURER_SETUP_CODE::UnknownException, "Unknown error");
	}
}

CapturerIntrinsics RS2Capturer::get_intrinsincs_from_stream(rs2::video_stream_profile profile) {
	auto intr = profile.get_intrinsics();
	unsigned int width = (unsigned int)intr.width;
	unsigned int height = (unsigned int)intr.height;
	return {width, height, static_cast<unsigned int>(intr.model), intr.ppx, intr.ppy, intr.fx, intr.fy, 
		{intr.coeffs[0], intr.coeffs[1], intr.coeffs[2], intr.coeffs[3], intr.coeffs[4]}
	}; 
}

CapturerIntrinsics RS2Capturer::get_depth_intrinsics() {
	if (!depth_sensor.has_value()) {
		return {};
	}
	auto profile = depth_sensor->get_active_streams()[0].as<rs2::video_stream_profile>();
	return get_intrinsincs_from_stream(profile);
}
CapturerIntrinsics RS2Capturer::get_color_intrinsics() {
	if(!color_sensor.has_value()) {
		return {};
	}
	auto profile = color_sensor->get_active_streams()[0].as<rs2::video_stream_profile>();
	return get_intrinsincs_from_stream(profile);
}
