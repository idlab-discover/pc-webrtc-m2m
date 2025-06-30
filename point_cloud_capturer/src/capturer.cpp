#include "capturer.hpp"
#include "log.h"
void Capturer::start_capturing()
{
    auto code = init();
    if(code == 0) {
        create_capture_worker();
    }
}


void Capturer::create_capture_worker()
{
    worker = std::thread(&Capturer::start_capturing_internal, this);
}

void Capturer::start_capturing_internal() {
    capture_done = false;
    keep_working = true;
    while (keep_working) {
        auto code = capture_next_frame();
        if (code != 0) {
            keep_working = false;
        }
    }
    
    std::unique_lock lk(m_capturing);
    capture_done = true;
    lk.unlock();
    cv_capture.notify_all();
}

void Capturer::wait_for_capture_done() {
    std::unique_lock lk(m_capturing);
	cv_capture.wait(lk, [this] { return capture_done; });
    if (worker.joinable())
        worker.join();
}


PointCloud *Capturer::poll_next_point_cloud()
{
    Frame* frame = poll_next_frame();
	if(frame == nullptr){
		return nullptr;
	}
	return new PointCloud{
		frame->get_timestamp(),
		frame->get_frame_nr(),
		frame->get_frame_size(),
		frame->get_vertex_array(),
		frame->get_color_array(),
		frame
	};
}

RawFrame *Capturer::poll_next_raw_frame()
{
    Frame* frame = poll_next_frame();
    if(frame == nullptr) {
        return nullptr;
    }
	return new RawFrame{
		frame->get_timestamp(),
        frame->get_capturer_id(),
		frame->get_frame_nr(),
		frame->get_capture_width(),
		frame->get_capture_height(),
		frame->get_raw_n_points(),
		frame->get_raw_depth(),
		frame->get_raw_colors(),
		frame
	};
}

void register_frame_ready_callback(Capturer* cap, FrameReadyCallback cb)
{
    if (cap == nullptr) {
        Log::custom_log("register_frame_ready_callback: Capturer is nullptr", Default, LogColor::Red);
        return;
    }
    cap->register_frame_ready_callback(cb); 
}