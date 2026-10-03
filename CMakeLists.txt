cmake_minimum_required(VERSION 3.24)
project(mirage_vr CXX)
set(CMAKE_CXX_STANDARD 20)

include(FetchContent)
FetchContent_Declare(minhook GIT_REPOSITORY https://github.com/TsudaKageyu/minhook.git GIT_TAG v1.3.3)
FetchContent_Declare(openxr  GIT_REPOSITORY https://github.com/KhronosGroup/OpenXR-SDK.git GIT_TAG release-1.0.34)
FetchContent_MakeAvailable(minhook openxr)

add_library(mirage_vr SHARED src/dllmain.cpp src/xr_runtime.cpp src/camera.cpp)
target_link_libraries(mirage_vr PRIVATE minhook openxr_loader d3d12 dxgi)
target_compile_definitions(mirage_vr PRIVATE XR_USE_GRAPHICS_API_D3D12 XR_USE_PLATFORM_WIN32)

add_executable(injector injector/injector.cpp)
