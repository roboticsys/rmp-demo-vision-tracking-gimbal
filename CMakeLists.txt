cmake_minimum_required(VERSION 3.15)

set(CMAKE_CXX_STANDARD 20)

project(LaserDemo)

# Include RSI SDK headers
include_directories(/rsi/examples/C++/include)

# Include Pylon SDK headers
include_directories(/opt/pylon/include)

# Find OpenCV
find_package(PkgConfig REQUIRED)
pkg_check_modules(OpenCV REQUIRED opencv4)
include_directories(${OpenCV_INCLUDE_DIRS})

# Find Pylon library
find_package(PYLON REQUIRED)

# Add your main source file
add_executable(LaserDemo src/laser_main.cpp)

# Set RMP Default Path
target_compile_definitions(LaserDemo PUBLIC RMP_DEFAULT_PATH="/rsi")

# Link libraries
target_link_libraries(LaserDemo PRIVATE pylon::pylon)
target_link_libraries(LaserDemo PRIVATE /rsi/librapidcode.so)
target_link_libraries(LaserDemo PRIVATE ${OpenCV_LIBRARIES})

# Optional flags
target_compile_options(LaserDemo PRIVATE "-Wno-deprecated-enum-enum-conversion")
