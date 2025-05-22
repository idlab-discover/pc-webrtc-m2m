set(_zdepth_SEARCHES)

# On Windows, we add the default install location

if(WIN32)
  list(APPEND _zdepth_SEARCHES "C:\\Program Files\\libzdepth")
#  set(CMAKE_FIND_LIBRARY_SUFFIXES .dll ${CMAKE_FIND_LIBRARY_SUFFIXES})
endif()

# Try each search configuration.
find_path(zdepth_INCLUDE_DIR NAMES include PATHS ${_zdepth_SEARCHES})
set(CMAKE_FIND_LIBRARY_SUFFIXES ".lib")
find_library(zdepth_LIB NAMES libzdepth PATHS ${_zdepth_SEARCHES} PATH_SUFFIXES lib)
message(PATHS ${_zdepth_SEARCHES} ${zdepth_LIB})

include(FindPackageHandleStandardArgs)
find_package_handle_standard_args(zdepth DEFAULT_MSG zdepth_LIB zdepth_INCLUDE_DIR)

if(zdepth_FOUND)
message("yes")
message( ${zdepth_LIB})
  set(zdepth_INCLUDE_DIRS "C:\\Program Files\\libzdepth\\include")
  set(zdepth_LIBRARIES ${zdepth_LIB})
  
  if(NOT TARGET zdepth::zdepth)
    add_library(zdepth::zdepth UNKNOWN IMPORTED)
    set_target_properties(zdepth::zdepth PROPERTIES
      INTERFACE_INCLUDE_DIRECTORIES "${zdepth_INCLUDE_DIRS}"
      )
    set_property(TARGET zdepth::zdepth APPEND PROPERTY
      IMPORTED_LOCATION "${zdepth_LIB}"
      )
  endif()
else()
message("no")
endif()