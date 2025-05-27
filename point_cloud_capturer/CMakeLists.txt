cmake_minimum_required(VERSION 3.18)

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

# Set the version properties for the library
set_target_properties(spirit_idlab_realsense PROPERTIES
    VERSION ${spirit_idlab_realsense_VERSION_MAJOR}.${spirit_idlab_realsense_VERSION_MINOR}.${spirit_idlab_realsense_VERSION_PATCH}
    SOVERSION ${spirit_idlab_realsense_VERSION_MAJOR}
)

# Set the CMake module path to find the custom FindREALSENSE2.cmake
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/CMakeFiles")

# Find the REALSENSE2 library
find_package(REALSENSE2 REQUIRED)
find_package(k4a REQUIRED)

# Link the REALSENSE2 library to the DLL
target_link_libraries(spirit_idlab_realsense ${REALSENSE2_LIBRARIES})
target_link_libraries(spirit_idlab_realsense ${k4a_LIBRARY} ${k4arecord_LIBRARY})

target_link_libraries(spirit_idlab_realsense winmm.lib)
# Include REALSENSE2 directories
include_directories(${CMAKE_SOURCE_DIR}/include ${CMAKE_SOURCE_DIR}/include/kinect ${CMAKE_SOURCE_DIR}/include/multi_capturer ${k4a_INCLUDE_DIR} ${REALSENSE2_INCLUDE_DIRS})