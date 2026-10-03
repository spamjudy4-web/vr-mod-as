#include "camera.h"

// TODO(reverse engineering): locate the camera/view-projection update in ACMirage.exe
// (RenderDoc capture -> find the constant buffer with the VP matrix -> trace the writer in x64dbg),
// then pattern-scan for it and hook with MinHook.
bool camera_install() { return false; }
void camera_set_eye(const EyeView&) {}
void camera_restore() {}
