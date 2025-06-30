#pragma once
#include "capturer.hpp"
#include "multi_capturer/multi_capturer.hpp"
class CapturerFactory {
    public:
      static CapturerFactory &get_instance();
  
      CapturerFactory(CapturerFactory const &) = delete;
      void operator=(const CapturerFactory &) = delete;
  
      ~CapturerFactory();
  
      Capturer* create_capturer(unsigned int capture_id, uint32_t fps, FrameMode mode, FrameCleanupSettings cleanup_settings, CAPTURE_TYPE type, void* capture_settings);
      MultiCapturer* create_multi_capturer(uint32_t fps, FrameMode mode, FrameCleanupSettings cleanup_settings, CAPTURE_TYPE type, unsigned int n_settings, void** capture_settings);
      private:
        CapturerFactory() = default;
      
  };
