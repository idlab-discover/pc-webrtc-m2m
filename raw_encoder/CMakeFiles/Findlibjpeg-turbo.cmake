set(_libjpeg-turbo_SEARCHES)

# On Windows, we add the default install location

if(WIN32)
  list(APPEND _libjpeg-turbo_SEARCHES "C:/Program Files/libjpeg")
#  set(CMAKE_FIND_LIBRARY_SUFFIXES .dll ${CMAKE_FIND_LIBRARY_SUFFIXES})
endif()

# Try each search configuration.
find_path(libjpeg-turbo_INCLUDE_DIR NAMES include PATHS ${_libjpeg-turbo_SEARCHES})
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
find_library(libjpeg-turbo_LIB NAMES turbojpeg PATHS ${_libjpeg-turbo_SEARCHES} PATH_SUFFIXES lib)
message( ${libjpeg-turbo_LIB})

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(libjpeg-turbo DEFAULT_MSG libjpeg-turbo_LIB libjpeg-turbo_INCLUDE_DIR)

if(libjpeg-turbo_FOUND)
message("yes")
message( ${libjpeg-turbo_LIB})
  set(libjpeg-turbo_INCLUDE_DIRS "C:/Program Files/libjpeg/include")
  set(libjpeg-turbo_LIBRARIES ${libjpeg-turbo_LIB})
  
  if(NOT TARGET libjpeg-turbo::turbojpeg)
    add_library(libjpeg-turbo::turbojpeg UNKNOWN IMPORTED)
    set_target_properties(libjpeg-turbo::turbojpeg PROPERTIES
      INTERFACE_INCLUDE_DIRECTORIES "${libjpeg-turbo_INCLUDE_DIRS}"
      )
    set_property(TARGET libjpeg-turbo::turbojpeg APPEND PROPERTY
      IMPORTED_LOCATION "${libjpeg-turbo_LIB}"
      )
  endif()
else()
message("no")
endif()