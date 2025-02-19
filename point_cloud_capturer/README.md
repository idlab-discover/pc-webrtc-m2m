# Spirit point cloud capturer
This repository contains the code to build a library that can be used in a Unity application (or any other programming environment that support Dlls) to capture point clouds.

## Building
This repository uses CMake to build the library. For Windows you can use the following command to generate the solution

 ```
 cmake -G "Visual Studio 17 2022" -A x64 -S . -B "x64" -DBUILD_SHARED_LIBS=ON -DCMAKE_CONFIGURATION_TYPES=Release
 ```
After this you will to open the generated solution, change the build type to `Release` and press `Ctrl+Shift+B` to build the  Dll.

:warning: Make sure you have the [Intel Realsense SDK 2](https://www.intelrealsense.com/sdk-2/) installed!


## Usage

##### To start capturing:
Setting `use_cam` to `false` will use an artifical point cloud cube (with the size of the side being equal to `artificial_size`) instead of the camera. 

`FrameMode ` determines how frames should be saved:

- 0 = Convert frame to point cloud
- 1 = Keep the raw color and depth frames
- 2 = Convert frame to point cloud and keep raw frames

 ```c++
initialize(uint32_t width, uint32_t height, uint32_t artificial_size, uint32_t fps, float min_dist, float max_dist, bool _use_cam, FrameMode mode)
 ```

##### To get the next point cloud (will only work with frame mode 0 & 2):
This method will block until the next frame or return a `nullptr` if the camera gets stopped before the next frame
```c++
PointCloud* poll_next_point_cloud()
```

##### You should free the point cloud to prevent memory leaks:
```c++
free_point_cloud(PointCloud * pc)
```

##### To get the next raw frame (will only work with frame mode 1 & 2):
This method will block until the next frame or return a `nullptr` if the camera gets stopped before the next frame

You can use this pointer in the provided `RawEncoder` library.

```c++
RawFrame* poll_next_raw_frame()
```

##### To get the number of points in the point cloud:
```c++
size_t get_point_cloud_size(PointCloud* frame)
```

##### You should free the raw frame to prevent memory leaks (after you are fully done with it):
```c++
free_raw_frame(RawFrame* frame)
```

##### To get the intrinsics of the capturer used to capture the color frame (with, height, focal point...):
```c++
CapturerIntrinsics get_color_intrinsics()
```

##### To get the intrinsics of the capturer used to capture the depth frame (with, height, focal point...):
```c++
CapturerIntrinsics get_depth_intrinsics()
```

##### To help with converting raw frames back into a point cloud you will need a converter:
```c++
RawConverter* create_new_raw_converter(bool use_cam, CapturerIntrinsics depth_intrinsics, CapturerIntrinsics color_intrinsics)
```

##### To convert raw depth and color data into formats suitable to render in Unity:

`pos_out` and `col_out` should be allocated on the Unity side and be pinned to make sure the method remains valid during excecution.

```c++
void convert_raw_frame(RawConverter* c, uint16_t* depth, uint8_t* color, Vector3* pos_out, Color32* col_out)
```

##### You should free the raw converter to prevent memory leaks (you only need to do this once at the end or whenever the intrinsics of the capturer changes):
```c++
void free_raw_converter(RawConverter* c)
```

##### To stop the cameras after you are done:
```c++
cleanup()
```

## Supported cameras
- Intel Realsense D series 

## Tested operating systems
- Windows 10/11

## Dependencies
- [Intel Realsense SDK 2](https://www.intelrealsense.com/sdk-2/)
