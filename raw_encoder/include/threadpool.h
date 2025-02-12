#pragma once
#include <vector>
#include <string>
#include <thread>
#include <condition_variable>
#include <queue>
#include <functional>
class ThreadPool {
public:
    void start(int num_threads);
    void queue_job(const std::function<void()>& job);
    void stop();
    bool busy();
    
private:
       void ThreadLoop();

    bool should_terminate = false;           // Tells threads to stop looking for jobs
    std::mutex queue_mutex;                  // Prevents data races to the job queue
    std::condition_variable mutex_condition; // Allows threads to wait on new jobs or termination 
    std::vector<std::thread> threads;
    std::queue<std::function<void()>> jobs;
    
};