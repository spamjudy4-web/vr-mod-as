#include <windows.h>
#include <d3d12.h>
#include <dxgi1_4.h>
#include <MinHook.h>
#include "xr_runtime.h"
#include "camera.h"

static XrRuntime g_xr;
static ID3D12CommandQueue* g_queue = nullptr;
static bool g_xrReady = false;

using ExecuteFn = void(STDMETHODCALLTYPE*)(ID3D12CommandQueue*, UINT, ID3D12CommandList* const*);
using PresentFn = HRESULT(STDMETHODCALLTYPE*)(IDXGISwapChain*, UINT, UINT);
static ExecuteFn oExecute;
static PresentFn oPresent;

static void STDMETHODCALLTYPE hkExecute(ID3D12CommandQueue* q, UINT n, ID3D12CommandList* const* l) {
    // The first DIRECT queue seen is assumed to be the game's main graphics queue.
    if (!g_queue && q->GetDesc().Type == D3D12_COMMAND_LIST_TYPE_DIRECT) g_queue = q;
    oExecute(q, n, l);
}

static HRESULT STDMETHODCALLTYPE hkPresent(IDXGISwapChain* sc, UINT sync, UINT flags) {
    if (!g_xrReady && g_queue) {
        ID3D12Device* dev = nullptr;
        g_queue->GetDevice(IID_PPV_ARGS(&dev));
        g_xrReady = dev && g_xr.init(dev, g_queue);
        camera_install();
    }
    if (g_xrReady) {
        EyeView eyes[2];
        if (g_xr.beginFrame(eyes)) {
            // TODO: for each eye: camera_set_eye -> let the game render -> g_xr.submitEye.
            // This needs the per-eye render loop designed around the camera hook.
            g_xr.endFrame();
        }
    }
    return oPresent(sc, sync, flags);
}

// Create a throwaway device/queue/swapchain to read the vtable addresses we need to hook.
static bool hookVtables() {
    WNDCLASSA wc{}; wc.lpfnWndProc = DefWindowProcA; wc.lpszClassName = "mvr_tmp";
    wc.hInstance = GetModuleHandleA(nullptr);
    RegisterClassA(&wc);
    HWND hwnd = CreateWindowA("mvr_tmp", "", 0, 0, 0, 8, 8, nullptr, nullptr, wc.hInstance, nullptr);

    ID3D12Device* dev = nullptr;
    if (FAILED(D3D12CreateDevice(nullptr, D3D_FEATURE_LEVEL_11_0, IID_PPV_ARGS(&dev)))) return false;
    D3D12_COMMAND_QUEUE_DESC qd{D3D12_COMMAND_LIST_TYPE_DIRECT};
    ID3D12CommandQueue* q = nullptr;
    dev->CreateCommandQueue(&qd, IID_PPV_ARGS(&q));
    IDXGIFactory4* fac = nullptr;
    CreateDXGIFactory1(IID_PPV_ARGS(&fac));
    DXGI_SWAP_CHAIN_DESC sd{};
    sd.BufferCount = 2; sd.BufferDesc.Format = DXGI_FORMAT_R8G8B8A8_UNORM;
    sd.BufferUsage = DXGI_USAGE_RENDER_TARGET_OUTPUT; sd.OutputWindow = hwnd;
    sd.SampleDesc.Count = 1; sd.Windowed = TRUE; sd.SwapEffect = DXGI_SWAP_EFFECT_FLIP_DISCARD;
    IDXGISwapChain* sc = nullptr;
    if (FAILED(fac->CreateSwapChain(q, &sd, &sc))) return false;

    void** qvt = *(void***)q;   // ExecuteCommandLists = index 10
    void** svt = *(void***)sc;  // Present = index 8
    bool ok = MH_Initialize() == MH_OK
        && MH_CreateHook(qvt[10], hkExecute, (void**)&oExecute) == MH_OK
        && MH_CreateHook(svt[8], hkPresent, (void**)&oPresent) == MH_OK
        && MH_EnableHook(MH_ALL_HOOKS) == MH_OK;
    sc->Release(); fac->Release(); q->Release(); dev->Release();
    DestroyWindow(hwnd);
    return ok;
}

static DWORD WINAPI init(LPVOID) { hookVtables(); return 0; }

BOOL APIENTRY DllMain(HMODULE h, DWORD reason, LPVOID) {
    if (reason == DLL_PROCESS_ATTACH) {
        DisableThreadLibraryCalls(h);
        CreateThread(nullptr, 0, init, nullptr, 0, nullptr);
    } else if (reason == DLL_PROCESS_DETACH) {
        g_xr.shutdown();
        MH_Uninitialize();
    }
    return TRUE;
}
