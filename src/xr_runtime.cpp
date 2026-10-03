#include "xr_runtime.h"

#define CHECK(x) do { if (XR_FAILED(x)) return false; } while (0)

bool XrRuntime::init(ID3D12Device* device, ID3D12CommandQueue* queue) {
    const char* ext[] = {XR_KHR_D3D12_ENABLE_EXTENSION_NAME};
    XrInstanceCreateInfo ci{XR_TYPE_INSTANCE_CREATE_INFO};
    strcpy_s(ci.applicationInfo.applicationName, "MirageVR");
    ci.applicationInfo.apiVersion = XR_CURRENT_API_VERSION;
    ci.enabledExtensionCount = 1;
    ci.enabledExtensionNames = ext;
    CHECK(xrCreateInstance(&ci, &instance_));

    XrSystemGetInfo si{XR_TYPE_SYSTEM_GET_INFO};
    si.formFactor = XR_FORM_FACTOR_HEAD_MOUNTED_DISPLAY;
    CHECK(xrGetSystem(instance_, &si, &system_));

    PFN_xrGetD3D12GraphicsRequirementsKHR getReq;
    CHECK(xrGetInstanceProcAddr(instance_, "xrGetD3D12GraphicsRequirementsKHR",
                                (PFN_xrVoidFunction*)&getReq));
    XrGraphicsRequirementsD3D12KHR req{XR_TYPE_GRAPHICS_REQUIREMENTS_D3D12_KHR};
    CHECK(getReq(instance_, system_, &req));

    XrGraphicsBindingD3D12KHR bind{XR_TYPE_GRAPHICS_BINDING_D3D12_KHR};
    bind.device = device;
    bind.queue = queue;
    XrSessionCreateInfo sci{XR_TYPE_SESSION_CREATE_INFO};
    sci.next = &bind;
    sci.systemId = system_;
    CHECK(xrCreateSession(instance_, &sci, &session_));

    XrReferenceSpaceCreateInfo rs{XR_TYPE_REFERENCE_SPACE_CREATE_INFO};
    rs.referenceSpaceType = XR_REFERENCE_SPACE_TYPE_STAGE;
    rs.poseInReferenceSpace.orientation.w = 1;
    CHECK(xrCreateReferenceSpace(session_, &rs, &space_));

    uint32_t n = 0;
    XrViewConfigurationView vcv[2]{{XR_TYPE_VIEW_CONFIGURATION_VIEW}, {XR_TYPE_VIEW_CONFIGURATION_VIEW}};
    CHECK(xrEnumerateViewConfigurationViews(instance_, system_,
        XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO, 2, &n, vcv));
    width = vcv[0].recommendedImageRectWidth;
    height = vcv[0].recommendedImageRectHeight;

    for (int e = 0; e < 2; ++e) {
        XrSwapchainCreateInfo sc{XR_TYPE_SWAPCHAIN_CREATE_INFO};
        sc.usageFlags = XR_SWAPCHAIN_USAGE_COLOR_ATTACHMENT_BIT | XR_SWAPCHAIN_USAGE_TRANSFER_DST_BIT;
        sc.format = 29; // DXGI_FORMAT_R8G8B8A8_UNORM_SRGB; should be chosen via xrEnumerateSwapchainFormats
        sc.sampleCount = 1; sc.width = width; sc.height = height;
        sc.faceCount = 1; sc.arraySize = 1; sc.mipCount = 1;
        CHECK(xrCreateSwapchain(session_, &sc, &swapchain_[e]));
        uint32_t cnt = 0;
        xrEnumerateSwapchainImages(swapchain_[e], 0, &cnt, nullptr);
        images_[e].assign(cnt, {XR_TYPE_SWAPCHAIN_IMAGE_D3D12_KHR});
        xrEnumerateSwapchainImages(swapchain_[e], cnt, &cnt,
            (XrSwapchainImageBaseHeader*)images_[e].data());
    }
    XrSessionBeginInfo bi{XR_TYPE_SESSION_BEGIN_INFO};
    bi.primaryViewConfigurationType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
    CHECK(xrBeginSession(session_, &bi));
    running_ = true;
    return true;
}

bool XrRuntime::beginFrame(EyeView out[2]) {
    if (!running_) return false;
    XrFrameWaitInfo wi{XR_TYPE_FRAME_WAIT_INFO};
    if (XR_FAILED(xrWaitFrame(session_, &wi, &frameState_))) return false;
    XrFrameBeginInfo bi{XR_TYPE_FRAME_BEGIN_INFO};
    if (XR_FAILED(xrBeginFrame(session_, &bi))) return false;

    XrViewLocateInfo li{XR_TYPE_VIEW_LOCATE_INFO};
    li.viewConfigurationType = XR_VIEW_CONFIGURATION_TYPE_PRIMARY_STEREO;
    li.displayTime = frameState_.predictedDisplayTime;
    li.space = space_;
    XrViewState vs{XR_TYPE_VIEW_STATE};
    views_[0].type = views_[1].type = XR_TYPE_VIEW;
    uint32_t n;
    if (XR_FAILED(xrLocateViews(session_, &li, &vs, 2, &n, views_))) return false;
    for (int e = 0; e < 2; ++e) out[e] = {views_[e].pose, views_[e].fov};
    return true;
}

void XrRuntime::submitEye(int eye, ID3D12GraphicsCommandList* cl, ID3D12Resource* src) {
    uint32_t idx;
    XrSwapchainImageAcquireInfo ai{XR_TYPE_SWAPCHAIN_IMAGE_ACQUIRE_INFO};
    xrAcquireSwapchainImage(swapchain_[eye], &ai, &idx);
    XrSwapchainImageWaitInfo wi{XR_TYPE_SWAPCHAIN_IMAGE_WAIT_INFO};
    wi.timeout = XR_INFINITE_DURATION;
    xrWaitSwapchainImage(swapchain_[eye], &wi);
    // TODO: resource barriers (src -> COPY_SOURCE, dst -> COPY_DEST) and size/format match,
    // otherwise use a scaling blit pass instead of CopyResource.
    cl->CopyResource(images_[eye][idx].texture, src);
    XrSwapchainImageReleaseInfo ri{XR_TYPE_SWAPCHAIN_IMAGE_RELEASE_INFO};
    xrReleaseSwapchainImage(swapchain_[eye], &ri);
}

void XrRuntime::endFrame() {
    XrCompositionLayerProjectionView pv[2]{};
    for (int e = 0; e < 2; ++e) {
        pv[e].type = XR_TYPE_COMPOSITION_LAYER_PROJECTION_VIEW;
        pv[e].pose = views_[e].pose;
        pv[e].fov = views_[e].fov;
        pv[e].subImage.swapchain = swapchain_[e];
        pv[e].subImage.imageRect = {{0, 0}, {(int32_t)width, (int32_t)height}};
    }
    XrCompositionLayerProjection layer{XR_TYPE_COMPOSITION_LAYER_PROJECTION};
    layer.space = space_; layer.viewCount = 2; layer.views = pv;
    const XrCompositionLayerBaseHeader* layers[] = {(XrCompositionLayerBaseHeader*)&layer};
    XrFrameEndInfo ei{XR_TYPE_FRAME_END_INFO};
    ei.displayTime = frameState_.predictedDisplayTime;
    ei.environmentBlendMode = XR_ENVIRONMENT_BLEND_MODE_OPAQUE;
    ei.layerCount = 1; ei.layers = layers;
    xrEndFrame(session_, &ei);
}

void XrRuntime::shutdown() {
    if (session_) xrDestroySession(session_);
    if (instance_) xrDestroyInstance(instance_);
    session_ = XR_NULL_HANDLE; instance_ = XR_NULL_HANDLE; running_ = false;
}
