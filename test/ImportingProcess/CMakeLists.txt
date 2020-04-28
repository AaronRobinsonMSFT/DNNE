cmake_minimum_required(VERSION 3.10)

project(ImportingProcess)

# Include the platform directory
include_directories(../../src/platform)

add_executable(ImportingProcess main.c)