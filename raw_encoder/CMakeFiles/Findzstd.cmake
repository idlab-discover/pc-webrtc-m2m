set(_zstd_SEARCHES)

# On Windows, we add the default install location

if(WIN32)
  list(APPEND _zstd_SEARCHES "C:\\Program Files\\libzstd")
#  set(CMAKE_FIND_LIBRARY_SUFFIXES .dll ${CMAKE_FIND_LIBRARY_SUFFIXES})
endif()

# Try each search configuration.
find_path(zstd_INCLUDE_DIR NAMES include PATHS ${_zstd_SEARCHES})
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
find_library(zstd_LIB NAMES libzstd PATHS ${_zstd_SEARCHES} PATH_SUFFIXES lib)
message(PATHS ${_zstd_SEARCHES} ${zstd_LIB})

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(zstd DEFAULT_MSG zstd_LIB zstd_INCLUDE_DIR)

if(zstd_FOUND)
message("yes")
message( ${zstd_LIB})
  set(zstd_INCLUDE_DIRS "C:\\Program Files\\libzstd\\include")
  set(zstd_LIBRARIES ${zstd_LIB})
  
  if(NOT TARGET zstd::zstd)
    add_library(zstd::zstd UNKNOWN IMPORTED)
    set_target_properties(zstd::zstd PROPERTIES
      INTERFACE_INCLUDE_DIRECTORIES "${zstd_INCLUDE_DIRS}"
      )
    set_property(TARGET zstd::zstd APPEND PROPERTY
      IMPORTED_LOCATION "${zstd_LIB}"
      )
  endif()
else()
message("no")
endif()