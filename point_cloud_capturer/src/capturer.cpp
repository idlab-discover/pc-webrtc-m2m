#include "capturer.hpp"
#include "log.h"
void Capturer::start_capturing(bool start_capture_thread)
{
    auto code = init();
    if(code == 0 && start_capture_thread) {
        create_capture_worker();
    }
}


void Capturer::create_capture_worker()
{
    capture_done = false;
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
	return frame->get_point_cloud();
}

RawFrame *Capturer::poll_next_raw_frame()
{
    Frame* frame = poll_next_frame();
    if(frame == nullptr) {
        return nullptr;
    }
	return frame->get_raw_frame();
}

void register_frame_ready_callback(Capturer* cap, FrameReadyCallback cb)
{
    if (cap == nullptr) {
        Log::custom_log("register_frame_ready_callback: Capturer is nullptr", Default, LogColor::Red);
        return;
    }

    cap->register_frame_ready_callback(cb); 
}