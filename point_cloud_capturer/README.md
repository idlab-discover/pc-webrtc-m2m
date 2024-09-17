# Spirit point cloud capturer
This repository contains the code to build a library that can be used in a Unity application (or any other programming environment that support Dlls) to capture point clouds.

## Building
This repository uses CMake to build the library. For Windows you can use the following command to generate the solution

 ```
 cmake -G "Visual Studio 17 2022" -A x64 -S . -B "x64" -DBUILD_SHARED_LIBS=ON -DCMAKE_CONFIGURATION_TYPES=Release
 ```
After this you will to open the generated solution, change the build type to `Release` and press `Ctrl+Shift+B` to build the  Dll.

## Usage

##### To start capturing:
Setting `use_cam` to `false` will use an artifical point cloud cube instead of the camera.
 ```c++
initialize(uint32_t width, uint32_t height, uint32_t fps, float min_dist, float max_dist, bool _use_cam)
 ```

##### To get the next point cloud:
This method will block until the next frame or return a `nullptr` if the camera gets stopped before the next frame
```c++
poll_next_point_cloud()
```

##### You should free the point cloud to prevent memory leaks:
```c++
free_point_cloud(PointCloud * pc)
```

##### To stop the cameras after you are done:
```c++
cleanup()
```

## Supported cameras
- Intel Realsense D series 

## Tested operating systems
- Windows 10/11

## Dependancies
- [Intel Realsense SDK 2](https://www.intelrealsense.com/sdk-2/)