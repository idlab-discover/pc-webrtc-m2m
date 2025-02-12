cmake_minimum_required(VERSION 3.18.0)


add_library(spirit_idlab_realsense 
	SHARED 
	plugin.cpp 
	dllmain.cpp 
	log.cpp 
	pch.cpp 
)

target_sources(spirit_idlab_realsense
	PRIVATE
	"../include/framework.h"
	"../include/log.h"
	"../include/pch.h"
	"../include/plugin.h"
)

target_link_libraries(spirit_idlab_realsense PRIVATE REALSENSE2::REALSENSE2)

install(TARGETS spirit_idlab_realsense
    EXPORT spirit_idlab_realsense
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
    PUBLIC_HEADER DESTINATION "include/spirit_idlab_realsense"
)

install(DIRECTORY ${PROJECT_SOURCE_DIR}/include/spirit_idlab_realsense
        DESTINATION ${CMAKE_INSTALL_INCLUDEDIR}
)

#install(FILES ${PROJECT_SOURCE_DIR}/CMakeFiles/cwipc_realsense2-config.cmake DESTINATION lib/cmake/cwipc_realsense2)

install(EXPORT spirit_idlab_realsense DESTINATION lib/cmake/spirit_idlab_realsense)
message(sdssdsd${${CMAKE_INSTALL_BINDIR}})
if(WIN32)
	# Copy the dependent DLLs that cmake/vcpkg have created
	install(FILES $<TARGET_RUNTIME_DLLS:spirit_idlab_realsense> DESTINATION ${CMAKE_INSTALL_BINDIR})
	# Copy the PDB file, if it exists
	install(FILES $<TARGET_PDB_FILE:spirit_idlab_realsense> DESTINATION ${CMAKE_INSTALL_BINDIR} OPTIONAL)
endif()