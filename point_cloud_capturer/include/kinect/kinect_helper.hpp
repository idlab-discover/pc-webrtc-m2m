#ifdef USE_KINECT
#include <k4a/k4a.h>
#include <k4arecord/playback.h>
#include "log.h"
class KinectHelper {
public:
  static void create_xy_table(const k4a_calibration_t &cal, bool align_to_depth, k4a_image_t &xy_table);

};
#endif