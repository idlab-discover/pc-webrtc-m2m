#include "encoding_queue.hpp"
#include "logging/logging_macros.hpp"

int EncodingQueue::enqueue_frame(RawFrame *f)
{
    
    std::unique_lock lk(m_enqueue);

    // Only buffer 1 frame extra
    // TODO maybe change to a vector of frames for subframes etc...
    if(current_in_wait != nullptr) {
        LOG_FRAME_STATUS_TO_FILE(NAME, Log::Status::Dropped, f->capturer_id, f->frame_nr);
        free_frame_callback_instance(current_in_wait);
        latest_dropped_frame_nr = current_in_wait->frame_nr;
    }
    if(f->frame_nr <= latest_dropped_frame_nr) {
        // Other subframes are already dropped, ignore
        LOG_FRAME_STATUS_TO_FILE(NAME, Log::Status::DroppedBecausePrevious, f->capturer_id, f->frame_nr);
        free_frame_callback_instance(f);
        lk.unlock();
        return 0;
    }

    current_in_wait = f;
   
    if(q_enqueued.size() < max_queue || latest_entered_frame_nr == f->frame_nr) {
        latest_entered_frame_nr = f->frame_nr;
        internal_enqueue_frame(current_in_wait);
    } 
    lk.unlock();
  
    return 0;
}

// TODO change to description pointer
void EncodingQueue::complete_encoding(EncodedRaw* f)
{
    std::unique_lock lk(m_enqueue);
    coding_status[f->get_capturer_id()][f->get_frame_nr()]--;
    if(coding_status[f->get_capturer_id()][f->get_frame_nr()] == 0) {
        q_enqueued.pop();
        coding_status[f->get_capturer_id()].erase(f->get_frame_nr());
        if(current_in_wait != nullptr) {
            internal_enqueue_frame(current_in_wait);
        }
        
    }
    lk.unlock();
    // TODO do description finished callback
    EncodedColor* enc_color = f->get_enc_color();
    EncodedDepth* enc_depth = f->get_enc_depth();
    depth_done_callback_instance(enc_depth->get_bytes(), enc_depth->get_size(), f->get_capturer_id(), f->get_frame_nr(), f->get_width(), f->get_height(), f->get_n_points(), f->get_timestamp());
    color_done_callback_instance(enc_color->get_bytes(), enc_color->get_size(), f->get_capturer_id(), f->get_frame_nr(), f->get_width(), f->get_height(), f->get_n_points(), f->get_timestamp());
}

void EncodingQueue::internal_enqueue_frame(RawFrame *f)
{
    LOG_FRAME_STATUS_TO_FILE_LIMITED(NAME, Log::Status::Enqueuing, f->capturer_id, f->frame_nr);
    coding_status[f->capturer_id][f->frame_nr] = 1;
    q_enqueued.push(true);
    current_in_wait = nullptr;
    pool.queue_job([this, f] {
        LOG_FRAME_STATUS_TO_FILE_LIMITED(NAME, Log::Status::Dequeued, f->capturer_id, f->frame_nr);
        EncodedRaw* enc_raw = this->enc.encode_raw(f);
        free_frame_callback_instance(f);
        this->complete_encoding(enc_raw);
        delete enc_raw;
    });
}

void register_color_done_callback(EncodingQueue* enc_queue, ColorDoneCallback cb)
{
    if(enc_queue == nullptr) {
        return;
    }
    enc_queue->register_color_done_callback(cb);
}

void register_depth_done_callback(EncodingQueue* enc_queue, DepthDoneCallback cb)
{
    if(enc_queue == nullptr) {
        return;
    }
    enc_queue->register_depth_done_callback(cb);
}

void register_free_frame_callback(EncodingQueue* enc_queue, FreeFrameCallback cb)
{
    if(enc_queue == nullptr) {
        return;
    }
    enc_queue->register_free_frame_callback(cb);
}
