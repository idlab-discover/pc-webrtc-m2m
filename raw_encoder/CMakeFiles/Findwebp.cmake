set(_webp_SEARCHES)

# On Windows, we add the default install location

if(WIN32)
  list(APPEND _webp_SEARCHES "C:\\Program Files\\libwebp")
#  set(CMAKE_FIND_LIBRARY_SUFFIXES .dll ${CMAKE_FIND_LIBRARY_SUFFIXES})
endif()

# Try each search configuration.
find_path(webp_INCLUDE_DIR NAMES include PATHS ${_webp_SEARCHES})
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
find_library(webp_LIB NAMES libwebp PATHS ${_webp_SEARCHES} PATH_SUFFIXES lib)
message( ${webp_LIB})

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(webp DEFAULT_MSG webp_LIB webp_INCLUDE_DIR)

if(webp_FOUND)
message("yes")
message( ${webp_LIB})
  set(webp_INCLUDE_DIRS "C:\\Program Files\\libwebp\\include")
  set(webp_LIBRARIES ${webp_LIB})
  
  if(NOT TARGET webp::webp)
    add_library(webp::webp UNKNOWN IMPORTED)
    set_target_properties(webp::webp PROPERTIES
      INTERFACE_INCLUDE_DIRECTORIES "${webp_INCLUDE_DIRS}"
      )
    set_property(TARGET webp::webp APPEND PROPERTY
      IMPORTED_LOCATION "${webp_LIB}"
      )
  endif()
else()
message("no")
endif()