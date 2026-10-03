# Mirage VR (work in progress)

Experimental OpenXR VR mod for **Assassin's Creed Mirage** (Anvil engine, DirectX 12, Windows).
Single-player only. Do not use with any online/multiplayer feature.

> **Status: scaffold, untested.** It was written without access to the game or a Windows
> toolchain. It has never been compiled or run. The hard part (finding the game camera and
> rendering a correct stereo pair) is still open — see "What's missing".

## How it works
1. `injector.exe` starts the game (or attaches to it) and loads `mirage_vr.dll`.
2. The DLL hooks `IDXGISwapChain::Present` and `ID3D12CommandQueue::ExecuteCommandLists`
   (MinHook) to capture the D3D12 device, command queue and back buffer.
3. It creates an OpenXR session using `XR_KHR_D3D12_ENABLE` bound to that device/queue.
4. Each frame: wait/begin XR frame, get head pose per eye, render the scene per eye,
   copy into the XR swapchain images, end frame.

## What's missing (needs reverse engineering on a real install)
- **Camera**: find the view/projection matrices (or camera object) in the Anvil engine so each
  eye can be rendered with its own offset/FOV. Place this in `src/camera.cpp`.
  Typical approach: Cheat Engine/x64dbg + RenderDoc to locate the constant buffer holding the
  view-projection matrix, then hook the function that writes it.
- **Stereo rendering**: simplest viable path is alternate-eye rendering (render the frame twice
  with different camera offsets, 2x CPU/GPU cost). Frame-pacing/TAA/temporal effects need care.
- **Input**: map OpenXR controllers to game input (XInput emulation is the easiest start).
- **UI/HUD**: render to a world-space quad layer instead of baking into the eye buffers.
- **Disable**: motion blur, TAA ghosting, screen-space effects that break in stereo.

Existing tools worth checking before investing further: Luke Ross's R.E.A.L. VR mod,
VorpX, and community Mirage VR efforts — they may already cover this.

## Build (Windows, MSVC)
Requires CMake, the OpenXR SDK and MinHook (fetched by CMake).
```
cmake -B build -A x64
cmake --build build --config Release
```
Outputs `mirage_vr.dll` and `injector.exe`. Run `injector.exe "C:\path\to\ACMirage.exe"`.
