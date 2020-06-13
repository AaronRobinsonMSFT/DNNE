cmake_minimum_required(VERSION 3.10)

project(ImportingProcess)

# Include the platform directory
include_directories(../../src/platform)

add_executable(ImportingProcess main.c)

if(UNIX AND NOT APPLE)
    target_link_libraries(ImportingProcess ${CMAKE_DL_LIBS})
endif()