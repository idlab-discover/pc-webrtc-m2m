set(_DRACO_SEARCHES)

# On Windows, we add the default install location

if(WIN32)
  list(APPEND _DRACO_SEARCHES "C:/Users/Administrator/Documents/PC-Streaming/draco/")
  list(APPEND _DRACO_SEARCHES "C:/Draco/")
  list(APPEND _DRACO_SEARCHES "C:/Program Files/draco")
#  set(CMAKE_FIND_LIBRARY_SUFFIXES .dll ${CMAKE_FIND_LIBRARY_SUFFIXES})
endif()

# Try each search configuration.
find_path(DRACO_INCLUDE_DIR NAMES draco/src PATHS ${_DRACO_SEARCHES})
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
find_library(DRACO_LIB NAMES draco PATHS ${_DRACO_SEARCHES} PATH_SUFFIXES lib)


include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(DRACO DEFAULT_MSG DRACO_LIB DRACO_INCLUDE_DIR)

if(DRACO_FOUND)
message("yes")
message( ${DRACO_LIB})
  set(DRACO_INCLUDE_DIRS "C:/Program Files/draco/include")
  set(DRACO_LIBRARIES ${DRACO_LIB})
  
  if(NOT TARGET DRACO::DRACO)
    add_library(DRACO::DRACO UNKNOWN IMPORTED)
    set_target_properties(DRACO::DRACO PROPERTIES
      INTERFACE_INCLUDE_DIRECTORIES "${DRACO_INCLUDE_DIRS}"
      )
    set_property(TARGET DRACO::DRACO APPEND PROPERTY
      IMPORTED_LOCATION "${DRACO_LIB}"
      )
  endif()
else()
message("no")
endif()