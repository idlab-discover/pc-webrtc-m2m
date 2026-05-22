cmake_minimum_required(VERSION 3.18)
include(FetchContent)

option(USE_REALSENSE "Include RealSense support" ON)
option(USE_KINECT    "Include Kinect (k4a) support" OFF)

# Define the project name and the type of project
project(spirit_idlab_realsense LANGUAGES CXX)

# Set the version of the library
set(spirit_idlab_realsense_VERSION_MAJOR 1)
set(spirit_idlab_realsense_VERSION_MINOR 0)
set(spirit_idlab_realsense_VERSION_PATCH 0)

# Specify the C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED True)



# Add the source directory
file(GLOB_RECURSE SOURCES "src/*.cpp")

# Set the output directory for the DLL
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# Add the library target
add_library(spirit_idlab_realsense SHARED ${SOURCES})
set_target_properties(spirit_idlab_realsense PROPERTIES PREFIX "")

# Set the version properties for the library
#set_target_properties(spirit_idlab_realsense PROPERTIES
#   VERSION ${spirit_idlab_realsense_VERSION_MAJOR}.${spirit_idlab_realsense_VERSION_MINOR}.${spirit_idlab_realsense_VERSION_PATCH}
#    SOVERSION ${spirit_idlab_realsense_VERSION_MAJOR}
#)

FetchContent_Declare(
  fast_float
  GIT_REPOSITORY https://github.com/fastfloat/fast_float.git
  GIT_TAG tags/v8.2.2
  GIT_SHALLOW TRUE)
FetchContent_MakeAvailable(fast_float)


# Set the CMake module path to find the custom FindREALSENSE2.cmake
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/CMakeFiles")

# Find the REALSENSE2 library
# --- RealSense Setup ---
if(USE_REALSENSE)
    message("Using realsense")
    find_package(REALSENSE2 REQUIRED)
    target_link_libraries(spirit_idlab_realsense PRIVATE ${REALSENSE2_LIBRARIES})
    target_include_directories(spirit_idlab_realsense PRIVATE ${REALSENSE2_INCLUDE_DIRS})
    target_compile_definitions(spirit_idlab_realsense PRIVATE USE_REALSENSE WITH_REALSENSE)
endif()

# --- Kinect Setup ---
if(USE_KINECT)
    find_package(k4a REQUIRED)
    target_link_libraries(spirit_idlab_realsense PRIVATE ${k4a_LIBRARY} ${k4arecord_LIBRARY})
    target_include_directories(spirit_idlab_realsense PRIVATE 
        ${CMAKE_SOURCE_DIR}/include/kinect 
        ${k4a_INCLUDE_DIR}
    )
    target_compile_definitions(spirit_idlab_realsense PRIVATE USE_KINECT WITH_KINECT)
endif()

# --- Platform Specific: Windows Multimedia ---
if(WIN32)
    target_link_libraries(spirit_idlab_realsense PRIVATE winmm.lib)
endif()

# --- Common setup ---
target_link_libraries(spirit_idlab_realsense PRIVATE fast_float)

target_include_directories(spirit_idlab_realsense PRIVATE
    ${CMAKE_SOURCE_DIR}/include 
    ${CMAKE_SOURCE_DIR}/include/utils 
    ${CMAKE_SOURCE_DIR}/include/multi_capturer 
    ${CMAKE_SOURCE_DIR}/include/external/plywoot 
    ${CMAKE_SOURCE_DIR}/include/multi_capturer/external/plywoot/plywoot
)
