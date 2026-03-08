cmake_minimum_required(VERSION 3.18)
project(spirit_idlab_draco LANGUAGES CXX)
set(CMAKE_POSITION_INDEPENDENT_CODE ON)
# --- 1. Automated Dependency Management (Replaces FindDRACO) ---
include(FetchContent)

FetchContent_Declare(
  draco
  GIT_REPOSITORY https://github.com/google/draco.git
  GIT_TAG        1.5.7  # Recommended stable version
)

# Optional: Disable Draco's internal tests/features to speed up your build
set(DRACO_BUILD_TESTS OFF CACHE BOOL "" FORCE)
set(DRACO_BUILD_EXAMPLES OFF CACHE BOOL "" FORCE)
set(DRACO_FAST ON CACHE BOOL "" FORCE)
set(DRACO_WASM OFF CACHE BOOL "" FORCE)
set(DRACO_INSTALL OFF CACHE BOOL "" FORCE)
set(DRACO_ENABLE_SSE4_1 ON CACHE BOOL "" FORCE)

FetchContent_MakeAvailable(draco)

# --- 2. Project Configuration ---
set(spirit_idlab_draco_VERSION_MAJOR 1)
set(spirit_idlab_draco_VERSION_MINOR 0)
set(spirit_idlab_draco_VERSION_PATCH 0)

set(CMAKE_CXX_STANDARD 17)
set(CMAKE_CXX_STANDARD_REQUIRED True)

# Set the output directory for the DLL
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)

# --- 3. Library Target ---
# It's better to list files explicitly, but keeping your GLOB for now
file(GLOB_RECURSE SOURCES "src/*.cpp")
add_library(spirit_idlab_draco SHARED ${SOURCES})
set_target_properties(spirit_idlab_draco PROPERTIES PREFIX "")
# --- 4. Linking and Includes ---
# target_include_directories is preferred over the global include_directories
target_include_directories(spirit_idlab_draco PRIVATE 
    "${draco_SOURCE_DIR}/src" 
    "${draco_BINARY_DIR}"
    "${PROJECT_BINARY_DIR}"
)

target_include_directories(spirit_idlab_draco PRIVATE include)

# Link Draco. FetchContent makes the 'draco' target available globally.
# This automatically handles include paths and library linking.
target_link_libraries(spirit_idlab_draco PRIVATE draco_static)

# --- 5. Target Properties ---
#set_target_properties(spirit_idlab_draco PROPERTIES
#    VERSION ${spirit_idlab_draco_VERSION_MAJOR}.${spirit_idlab_draco_VERSION_MINOR}.${spirit_idlab_draco_VERSION_PATCH}
#    SOVERSION ${spirit_idlab_draco_VERSION_MAJOR}
#)