# Slime Rancher VR

Experimental VR mod for **Slime Rancher 1** (Unity, Mono, Windows), built as a
[BepInEx 5](https://github.com/BepInEx/BepInEx) plugin that talks to **OpenVR/SteamVR** directly
(works with Index, Vive, and Quest via Link / Virtual Desktop / Air Link through SteamVR).

> **Status: untested.** Written without access to the game or a .NET toolchain, so it has not
> been compiled or run. Expect to fix compile errors and tune things on first launch.

## How it works
Each frame the plugin renders the game's main camera twice (once per eye) into RenderTextures,
using the headset's per-eye projection and offset, and submits them to the SteamVR compositor.
The headset pose is added on top of the game's own camera, so mouse/gamepad look and walking
still drive the player. The left eye is mirrored to the desktop window. Motion controllers drive movement, buttons and aiming (below).

## Install
1. Install **SteamVR** and the 64-bit **BepInEx 5.4.x** (x64) into the Slime Rancher folder; run the game once.
2. Build: `dotnet build SlimeRancherVR -c Release -p:GameDir="C:\...\Slime Rancher\"`
   (copies `SlimeRancherVR.dll` to `BepInEx\plugins` and `openvr_api.dll` to `SlimeRancher_Data\Plugins`).
3. Start SteamVR, then the game. **Turn V-Sync off** in the game's options (the headset sets the pace).
4. Settings are in `BepInEx\config\com.spamjudy4.slimerancher.vr.cfg`.

## Controls (motion controllers)
Works with any controller SteamVR exposes (Touch, Index, Vive, WMR). Buttons are turned into
keyboard/mouse input at OS level, so it doesn't depend on the game's input system.

| Input | Default action |
|---|---|
| Right controller | Aim (the camera is steered to where you point; see below) |
| Right trigger / grip | Mouse left / right |
| Right A / B | Space (jump) / F |
| Left stick | W A S D |
| Left trigger / grip / A / B | Left Shift / E / R / Tab |
| Right stick left/right | Snap turn (30 deg) |
| Right stick up/down | Mouse wheel (cycle hotbar) |
| `F9` / `F10` | Recenter / toggle hand aim |

**The defaults are guesses at the game's default bindings; remap them in the `[Controls]` section of the config.**

**How hand aim works:** the game's camera aim is controlled by sending relative mouse movement until the
camera points where the right controller does (learning the mouse gain as it goes). The view you see is
detached from that, using its own snap-turn yaw, so aiming doesn't spin the world around you.
Consequences: movement is relative to where the right hand points, and aim lags the hand by a frame or two.
If it misbehaves, press `F10` to fall back to mouse aim, or set `HandAim = false`.

## Known limitations / next steps
- **Controls are untested** and the biggest risks are: wrong default key bindings, the aim loop
  (sign/gain learning) behaving oddly with your mouse settings, and the game ignoring injected input.
- **Menus:** controller buttons click, but the cursor isn't moved by the controller yet.
- **Movement is digital** (WASD), not analog, and relative to the aim hand rather than the head.
- **HUD isn't in the headset.** Screen-space UI only appears on the monitor; it needs a world-space canvas.
- **Post-processing is missing** on the eye cameras (they copy the camera settings, not its effect components).
- **Possible submit timing issues.** Textures are submitted from the main thread after rendering;
  Unity's render thread may lag, causing flicker. A native plugin submitting on the render thread is the proper fix.
- **Projection matrix handedness** (`OpenVRSession.ToUnity(HmdMatrix44_t)`) is unverified; if the image is
  distorted or inverted, check that first.
- Slime Rancher 2 is not supported (IL2CPP).
- Third-party: OpenVR (`ThirdParty/`, BSD-3-Clause, see `OPENVR_LICENSE`).
