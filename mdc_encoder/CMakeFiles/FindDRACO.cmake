set(_DRACO_SEARCHES)

# On Windows, we add the default install location

if(WIN32)
  list(APPEND _DRACO_SEARCHES "C:/Users/Administrator/Documents/PC-Streaming/draco/")
  list(APPEND _DRACO_SEARCHES "C:/Draco/")
#  set(CMAKE_FIND_LIBRARY_SUFFIXES .dll ${CMAKE_FIND_LIBRARY_SUFFIXES})
endif()

# Try each search configuration.
find_path(DRACO_INCLUDE_DIR NAMES draco/src PATHS ${_DRACO_SEARCHES})
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
find_library(DRACO_LIB NAMES draco PATHS ${_DRACO_SEARCHES} PATH_SUFFIXES x64/Release)


include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(DRACO DEFAULT_MSG DRACO_LIB DRACO_INCLUDE_DIR)

if(DRACO_FOUND)
message("yes")
message( ${DRACO_LIB})
  set(DRACO_INCLUDE_DIRS "C:/Draco/draco/src")
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