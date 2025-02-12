#include "encoding_queue.hpp"


int EncodingQueue::enqueue_frame(RawFrame *f)
{
    
    std::unique_lock lk(m_enqueue);
    if(current_in_wait != nullptr) {
        free_frame_callback_instance(current_in_wait);
    }
    current_in_wait = f;

    if(q_enqueued.size() < max_queue) {
        internal_enqueue_frame(current_in_wait);
    } 
    lk.unlock();
  
    return 0;
}

// TODO change to description pointer
void EncodingQueue::complete_encoding(EncodedRaw* f)
{
    std::unique_lock lk(m_enqueue);
    coding_status[f->get_frame_nr()]--;
    if(coding_status[f->get_frame_nr()] == 0) {
        q_enqueued.pop();
        coding_status.erase(f->get_frame_nr());
        if(current_in_wait != nullptr) {
            internal_enqueue_frame(current_in_wait);
        }
        
    }
    lk.unlock();
    // TODO do description finished callback
    EncodedJpeg* enc_jpeg = f->get_enc_color();
    EncodedDepth* enc_depth = f->get_enc_depth();
    depth_done_callback_instance(enc_depth->get_bytes(), enc_depth->get_size(), f->get_frame_nr(), f->get_width(), f->get_height(), f->get_n_points(), f->get_timestamp());
    color_done_callback_instance(enc_jpeg->get_bytes(), enc_jpeg->get_size(), f->get_frame_nr(), f->get_width(), f->get_height(), f->get_n_points(), f->get_timestamp());
}

void EncodingQueue::internal_enqueue_frame(RawFrame *f)
{
    coding_status[f->frame_nr] = 1;
    q_enqueued.push(true);
    current_in_wait = nullptr;
    pool.queue_job([this, f] {
        EncodedRaw* enc_raw = this->enc.encode_raw(f);
        free_frame_callback_instance(f);
        this->complete_encoding(enc_raw);
        delete enc_raw;
    });
}


void register_color_done_callback(ColorDoneCallback cb)
{
    color_done_callback_instance = cb;
}

void register_depth_done_callback(DepthDoneCallback cb)
{
    depth_done_callback_instance = cb;
}

void register_free_frame_callback(FreeFrameCallback cb)
{
    free_frame_callback_instance = cb;
}
