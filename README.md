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
still drive the player. The left eye is mirrored to the desktop window.

## Install
1. Install **SteamVR** and the 64-bit **BepInEx 5.4.x** (x64) into the Slime Rancher folder; run the game once.
2. Build: `dotnet build SlimeRancherVR -c Release -p:GameDir="C:\...\Slime Rancher\"`
   (copies `SlimeRancherVR.dll` to `BepInEx\plugins` and `openvr_api.dll` to `SlimeRancher_Data\Plugins`).
3. Start SteamVR, then the game. **Turn V-Sync off** in the game's options (the headset sets the pace).
4. `F9` recenters. Settings are in `BepInEx\config\com.spamjudy4.slimerancher.vr.cfg`.

## Known limitations / next steps
- **No VR controllers yet.** Play with mouse/keyboard or a gamepad. Next: motion-controller aiming for the vacpack.
- **Aim follows the mouse, not your head.** Head movement only changes what you see.
- **HUD isn't in the headset.** The Screen-Space UI only appears on the monitor; it needs to be
  moved onto a world-space canvas in front of the player.
- **Post-processing is missing** on the eye cameras (they copy the camera settings, not its effect components).
- **Possible submit timing issues.** Textures are submitted from the main thread after rendering;
  Unity's render thread may lag, causing flicker. A native plugin submitting on the render thread is the proper fix.
- **Projection matrix handedness** (`OpenVRSession.ToUnity(HmdMatrix44_t)`) is unverified; if the image is
  distorted or inverted, check that first.
- Slime Rancher 2 is not supported (IL2CPP).
- Third-party: OpenVR (`ThirdParty/`, BSD-3-Clause, see `OPENVR_LICENSE`).
