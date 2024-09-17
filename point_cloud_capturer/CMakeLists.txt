cmake_minimum_required(VERSION 3.18)
option(ENABLE_VCPKG OFF)

# Define the project name and the type of project
project(spirit_idlab_draco LANGUAGES CXX)

# Set the version of the library
set(spirit_idlab_draco_VERSION_MAJOR 1)
set(spirit_idlab_draco_VERSION_MINOR 0)
set(spirit_idlab_draco_VERSION_PATCH 0)

# Specify the C++ standard
set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)

# Add the include directory
include_directories(include)

# Add the source directory
file(GLOB SOURCES "src/*.cpp")

# Set the output directory for the DLL
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# Add the library target
add_library(spirit_idlab_draco SHARED ${SOURCES})

# Set the version properties for the library
set_target_properties(spirit_idlab_draco PROPERTIES
    VERSION ${spirit_idlab_draco_VERSION_MAJOR}.${spirit_idlab_draco_VERSION_MINOR}.${spirit_idlab_draco_VERSION_PATCH}
    SOVERSION ${spirit_idlab_draco_VERSION_MAJOR}
)

# Set the CMake module path to find the custom FindREALSENSE2.cmake
list(APPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/CMakeFiles")

# Find the REALSENSE2 library
find_package(DRACO REQUIRED)
message(${DRACO_INCLUDE_DIRS})
# Link the REALSENSE2 library to the DLL
target_link_libraries(spirit_idlab_draco ${DRACO_LIBRARIES})

# Include REALSENSE2 directories
include_directories(${DRACO_INCLUDE_DIRS})
