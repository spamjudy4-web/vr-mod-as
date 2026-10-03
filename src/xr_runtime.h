#pragma once
#include <d3d12.h>
#include <windows.h>
#include <unknwn.h>
#include <openxr/openxr.h>
#include <openxr/openxr_platform.h>
#include <vector>

struct EyeView { XrPosef pose; XrFovf fov; };

class XrRuntime {
public:
    bool init(ID3D12Device* device, ID3D12CommandQueue* queue);
    // Returns true if VR frame is active; fills the two eye views.
    bool beginFrame(EyeView out[2]);
    // Copy `src` (game render target) into the eye's swapchain image.
    void submitEye(int eye, ID3D12GraphicsCommandList* cl, ID3D12Resource* src);
    void endFrame();
    void shutdown();
    uint32_t width = 0, height = 0;

private:
    XrInstance instance_ = XR_NULL_HANDLE;
    XrSystemId system_ = XR_NULL_SYSTEM_ID;
    XrSession session_ = XR_NULL_HANDLE;
    XrSpace space_ = XR_NULL_HANDLE;
    XrSwapchain swapchain_[2]{};
    std::vector<XrSwapchainImageD3D12KHR> images_[2];
    XrFrameState frameState_{XR_TYPE_FRAME_STATE};
    XrView views_[2]{};
    bool running_ = false;
};
