#include "encoding_queue.hpp"
#include "uniform_sampler.hpp"

int EncodingQueue::enqueue_pc(PointCloud *pc)
{
   // if(free_pc_callback_instance != nullptr)
    std::unique_lock lk(m_enqueue);
    if(current_in_wait != nullptr) {
        free_pc_callback_instance(current_in_wait);
    }
    current_in_wait = pc;
   // cv_enqueue.notify_all();
	//cv_enqueue.wait(lk, [&current_in_wait, pc] {
 //       return q_enqueued.size() < max_queue || current_in_wait != pc; 
 //   });
    if(q_enqueued.size() < max_queue) {
        // TODO enqueue job
        //      * Split in 3 descriptions DONE
        //      * Enqueue all jobs DONE
        //      * Add n_descriptions to map DONE
        //      * Rescale to max_points
        internal_enqueue_pc(current_in_wait);
    } 
    lk.unlock();
  
    return 0;
}

// TODO change to description pointer
void EncodingQueue::complete_encoding(Description* dsc)
{
    std::unique_lock lk(m_enqueue);
    coding_status[dsc->frame_nr]--;
    if(coding_status[dsc->frame_nr] == 0) {
        q_enqueued.pop();
        coding_status.erase(dsc->frame_nr);
        if(current_in_wait != nullptr) {
            internal_enqueue_pc(current_in_wait);
        }
        
    }
    lk.unlock();
    // TODO do description finished callback
    description_done_callback_instance(dsc, dsc->enc->get_raw_data(), dsc->n_points_in_total, dsc->enc->get_encoded_size(), dsc->frame_nr, dsc->description_nr);
}

void EncodingQueue::internal_enqueue_pc(PointCloud *pc)
{
    UniformSampler us(std::vector<float>({ 0.6, 0.25, 0.15 }));
    std::vector<Description*> descs = us.create_descriptions(pc);
    coding_status[pc->frame_nr] = 3;
    q_enqueued.push(true);
    free_pc_callback_instance(current_in_wait);
    current_in_wait = nullptr;
    for(auto dsc : descs) {
        Description* dsc_copy = dsc;
        pool.queue_job([this, dsc_copy] {
            dsc_copy->enc = new DracoMDCEncoder();
            dsc_copy->enc->encode_pc(dsc_copy);
            this->complete_encoding(dsc_copy);
        });
    }
}


void register_description_done_callback(DescriptionDoneCallback cb)
{
    description_done_callback_instance = cb;
}

void register_free_pc_callback(FreePointCloudCallback cb)
{
    free_pc_callback_instance = cb;
}
