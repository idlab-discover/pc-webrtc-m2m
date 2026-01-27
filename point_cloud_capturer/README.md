# Spirit point cloud capturer
This repository contains the code to build a library that can be used in a Unity application (or any other programming environment that support Dlls) to capture point clouds.

Certain parts of the Kinect code is based on / inspired by [cwi_kinect](https://github.com/cwi-dis/cwipc_kinect).

You can also find an example on how to use the library in Unity at: [ExampleCamera](../unity/spirit_m2m_webrtc/Examples/SimpleCamera.md)

## Building
This repository uses CMake to build the library. For Windows you can use the following command to generate the solution. If you don't want to build the library yourself you can also use a [prebuilt version](../unity/spirit_m2m_webrtc/Assets/Plugins/spirit_idlab_realsense.dll).

 ```cpp
 cmake -G "Visual Studio 17 2022" -A x64 -S . -B "x64" -DBUILD_SHARED_LIBS=ON -DCMAKE_CONFIGURATION_TYPES=Release
 ```
After this you will to open the generated solution, change the build type to `Release` and press `Ctrl+Shift+B` to build the  Dll.

:warning: Make sure you have the [Intel Realsense SDK 2](https://www.intelrealsense.com/sdk-2/) and [Azure Kinect SDK v1.4.1](https://www.microsoft.com/en-us/download/details.aspx?id=101454) installed!


## Usage
The library contains two types of a capturers, a basic capturer for a single camera and a multi capturer when using multiple camera's (this one currently only has support for recorded Kinect camera files but can also be with a single Realsense camera). Also has the option to read ply files.

### Basic Capturer
You will first have to create a new capturer using the following function:

```
Capturer* create_new_capturer(uint32_t fps, FrameMode mode, FrameCleanupSettings cleanup_settings,
                              CAPTURE_TYPE type, void* capture_settings)
```

Arguments:

| Argument | Description |
|----------|-------------|
| `FrameMode` | Can either be 0 (RealData), 1 (RawData). You can use this to determine if the capturer should automatically convert the data into a point cloud (RealData) or if you want to do this manually (RawData) |
| `FrameCleanupSettings` | A struct containing `unsigned int blackout_block_size`, `bool should_apply_depth_filter`, `bool should_cleanup_depth` and `bool should_blackout`. You can use this struct to automatically remove pixels from the raw depth/color images that are too far away. To improve color encoding efficiency pixels, will only set to black if all pixels in grid of `blackout_block_size` are all too far away. The parameter `should_cleanup_depth` is very experimental and should be put to false.
| `CAPTURE_TYPE` | Can either be 0 (artificial), 1 (Realsense), 2 (PrerecordedRealsense), 3 (Kinect), 4 (PrerecordedKinect), 5 (Ply Files), and allows you to set which types of cameras you are using, with artificial being a cube that requires no cameras. At the moment only 0, 1 and 4 should be used.
| `capture_settings` | This is a pointer to a struct containing settings that are specific to a certain type of cameras. The different types of settings are listed below.

##### Artificial
| Variable | Description |
|----------|-------------|
| `unsigned int side_size` | This determines size of the sides of the cube |

##### Realsense
| Variable | Description |
|----------|-------------|
| `unsigned int width` | The width of captured image (currently has to be the same for both depth and color) |
| `unsigned int height` | The height of captured image (currently has to be the same for both depth and color) |
| `float min_dist` | The minimum capture distance, all points outside this range will be removed |
| `float max_dist` | The maximum capture distance, all points outside this range will be removed |
| `bool align_to_depth | Setting this to true will align the color image to the depth image, otherwise the opposite is performed. Depending on the camera model this needs to be either set on true/false if you see weird results in the image/point cloud.

##### Prerecorded Kinect
| Variable | Description |
|----------|-------------|
| `bool align_to_depth | Setting this to true will align the color image to the depth image, otherwise the opposite is performed. Depending on the camera model this needs to be either set on true/false if you see weird results in the image/point cloud.
| `float min_height` | Currently does nothing, but will be used in the future to remove specific points |
| `float max_height` | Currently does nothing, but will be used in the future to remove specific points |
| `float radius` | Currently does nothing, but will be used in the future to remove specific points |
| `float trafo[4][4]` | Array containing the rotational data of the camera, used to stitch the data over several cameras together (e.g., as seen in [certain datasets](https://www.dis.cwi.nl/cwipc-sxr-dataset/downloads/) |
| `char cam_file[256]`| Path to the captured camera data |

##### Ply Files (🚧Needs better parser though)
| Variable | Description |
|----------|-------------|
| `char directory_path[256]`| Path to the directory containing the .ply files. |

Be sure to free the capturer once you are done.

```
void free_capturer(Capturer* capturer)
```

##### Start capturing
To start the capturer you will need to use the function below. This function also has an optional parameter if you want to start a separate thread or not, currently if you do not set this to true you will need to use `get_single_frame(Capturer* capturer)` to get the next captured frame set.

```cpp
void start_capturing(Capturer* capturer, bool start_capture_thread)
```

##### Polling a point cloud
The function below will wait until the next point cloud is available, or return the `nullptr`if the camera was stopped. 

```cpp
PointCloud* poll_next_point_cloud(Capturer* capturer)
```
The returned pointer can the be used with the following functions:

| Function | Description |
|----------|-------------|
| `size_t get_point_cloud_size` | Returns the number of points in the point cloud |
| `uint64_t get_point_cloud_timestamp` | Returns the timestamp when the point cloud was captured |
| `unsigned int get_point_cloud_frame_nr`| Returns the frame number of the point cloud. |
| `Vertex* get_point_cloud_pos` | Returns a pointer to the array containing the position of each point. A `Vertex` a struct with `float x, y, z`. However, you can also just treat the array as an array of floats containing 3xNPoints. |
| `Color* get_point_cloud_col` | Returns a pointer to the array containing the color of each point. Similar to how the positions work but with `Color` being a struct of `uint8_t r, g, b` |
| `PointCloud* downsample_pc_random(PointCloud* pc, unsigned int max_points, bool create_new_pc)` | Downsamples the point cloud to a specific number of points. Can either choose to create a new point cloud (ensuring the current pointer remains valid) or overwrite the positions/colors from the old one. |
| `bool save_pc_to_ply(const char* file_path, PointCloud* pc)` | Saves the point cloud to a binary .ply file. |
| `void free_point_cloud` | Once you are done with the point cloud you should call this function or you will have memory leaks! |

#### Polling a frame
Instead of directly polling a point cloud, you can also just poll the captured frames.
```cpp
Frame* poll_next_frame(Capturer* capturer)
```

The returned pointer can the be used with the following functions:

| Function | Description |
|----------|-------------|
| `size_t get_frame_size` | Returns the number of data points in the frame |
| `uint16_t* get_raw_depth` | Returns a pointer to the array containing the raw depth data. This is data directly returned from the camera, and therefore requires additionall processing if you want to get the actual depth of each point |
| `uint8_t* get_raw_color` | Returns a pointer to the array containing the raw depth data. Each color has RGB channels so this array contains 3xNPoints of values |
| `PointCloud* convert_frame_to_pc` | Converts the frame into a point cloud, allowing you to use the functions above. Once you do this you no longer will need to manually free the `Frame*`as freeing the `PointCloud*`will do it for you |
| `RawFrame* convert_frame_to_raw_frame` | Converts the frame into a point cloud, allowing you to use the functions below. This essentially wraps the `Frame*`into a struct that makes it easier to use for other libraries. Only use this if you know what you are doing |
| `void free_frame` | Once you are done with the frame you should call this function (unless you converted the pointer) or you will have memory leaks! |

#### Polling a raw frame
You can also poll a raw frame which is essentially a struct wrapped around a `Frame*`for easy access to the raw depth and color data. Only use this if you know what you are doing.

```cpp
RawFrame* poll_next_raw_frame(Capturer* capturer)
```

The returned pointer can the be used with the following functions:

| Function | Description |
|----------|-------------|
| `void free_raw_frame` | Once you are done with the raw frame you should call this function or you will have memory leaks! |

### Multi Capturer
You will first have to create a new capturer using the function below. The parameters are similar to the basic capturer, with the exception that you now need to provide an array of `capture_settings`.

```
MultiCapturer* create_new_multi_capturer(uint32_t fps, FrameMode mode, FrameCleanupSettings cleanup_settings, 
		                        CAPTURE_TYPE type, unsigned int n_settings, void** capture_settings);
```

Be sure to free the capturer once you are done.

```
void free_multi_capturer(MultiCapturer* capturer)
```

#### Start capturing
To start the capturer you will need to use the function below. This function also has an optional parameter if you want to start a separate thread or not. If you do not set this you will need to use `get_single_combined_point_cloud(MultiCapturer* capturer)` to get access to the next point cloud.

```cpp
void start_capturing_multi(MultiCapturer* capturer, bool start_capture_thread)
```

#### Polling a combined point cloud
You can poll a point cloud combined from all active cameras using the function below. This return `PointCloud*`behaves similar to those returned from a basic capturer, and the same functions can be used with it.

```
PointCloud* get_single_combined_point_cloud(MultiCapturer* capturer)
```

#### Accessing individual camera data
You can also access the data of each camera individually using the functions below.

```
PointCloud* poll_next_point_cloud_for_capturer(MultiCapturer* capturer, unsigned int capturer_index)
Frame* poll_next_frame_for_capturer(MultiCapturer* capturer, unsigned int capturer_index)
RawFrame* poll_next_raw_frame_for_capturer(MultiCapturer* capturer, unsigned int capturer_index)
```


## Supported cameras
- Intel Realsense D series
- Prerecorded Kinect camera videos

## Tested operating systems
- Windows 10/11

## Dependencies
- [Intel Realsense SDK 2](https://www.intelrealsense.com/sdk-2
- [Azure Kinect SDK v1.4.1](https://www.microsoft.com/en-us/download/details.aspx?id=101454)
