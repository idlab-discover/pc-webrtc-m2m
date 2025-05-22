#include "capturer.hpp"
void Capturer::start_capturing() {
    auto code = init();
    if(code == 0) {
        worker = std::thread(&Capturer::start_capturing_internal, this);
    }
    
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