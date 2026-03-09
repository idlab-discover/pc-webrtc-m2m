cmake_minimum_required(VERSION 3.15)
project(WebRTCConnector VERSION 1.0.0 LANGUAGES CXX)

# Set C++ standard
set(CMAKE_CXX_STANDARD 20)
set(CMAKE_CXX_STANDARD_REQUIRED ON)

# Set output directories
set(CMAKE_RUNTIME_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/bin)
set(CMAKE_LIBRARY_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)
set(CMAKE_ARCHIVE_OUTPUT_DIRECTORY ${CMAKE_BINARY_DIR}/lib)

# Include directories
include_directories(${CMAKE_SOURCE_DIR}/include)

# Source files
set(COMMON_SOURCES
    src/log.cpp
    src/track_frame.cpp
    src/track_frame_queue.cpp
    src/track_internal.cpp
    src/webrtc_connection.cpp
    src/connected_client.cpp
    src/plugin.cpp
)


set(PLATFORM_SOURCES
    src/pch.cpp
)

# Find WebRTC (adjust path or method according to your setup)
# Uncomment and modify the following lines when you have WebRTC SDK installed
# find_package(WebRTC REQUIRED)

# Detect platform
if(WIN32)
    list(APPEND PLATFORM_SOURCES src/dllmain.cpp)
    set(PLATFORM_SPECIFIC_LIBS ws2_32)  # Windows sockets
    message(STATUS "Building for Windows")
elseif(APPLE)
    list(APPEND FRAMEWORK_LIBS "-framework CoreFoundation" "-framework AVFoundation")
    message(STATUS "Building for macOS")
elseif(LINUX)
    message(STATUS "Building for Linux")
endif()

# Create a shared library with common components
add_library(webrtc_connector_lib SHARED
    ${COMMON_SOURCES}
)
set_target_properties(webrtc_connector_lib PROPERTIES PREFIX "")

target_include_directories(webrtc_connector_lib PUBLIC
    ${CMAKE_SOURCE_DIR}/include
)

# Test executable (optional)
option(BUILD_TESTS "Build test executables" ON)

if(BUILD_TESTS)
    add_executable(track_frame_queue_test
        src/track_frame_queue2_test.cpp
    )
    
    target_link_libraries(track_frame_queue_test PRIVATE
        webrtc_connector_lib
    )
    
    target_include_directories(track_frame_queue_test PUBLIC
        ${CMAKE_SOURCE_DIR}/include
    )
    
    enable_testing()
    add_test(NAME TrackFrameQueueTest COMMAND track_frame_queue_test)
endif()

# Installation targets (optional)
install(TARGETS webrtc_connector_lib
    LIBRARY DESTINATION lib
    ARCHIVE DESTINATION lib
    RUNTIME DESTINATION bin
)

install(DIRECTORY ${CMAKE_SOURCE_DIR}/include/
    DESTINATION include
    FILES_MATCHING PATTERN "*.h" PATTERN "*.hpp"
)

# Print build summary
message(STATUS "")
message(STATUS "WebRTC Connector Build Summary")
message(STATUS "==============================")
message(STATUS "Project: ${PROJECT_NAME} v${PROJECT_VERSION}")
message(STATUS "Build Type: ${CMAKE_BUILD_TYPE}")
message(STATUS "Build Tests: ${BUILD_TESTS}")
message(STATUS "Platform: ${CMAKE_SYSTEM_NAME}")
message(STATUS "Compiler: ${CMAKE_CXX_COMPILER_ID}")
message(STATUS "")
