#pragma once
#include "xr_runtime.h"

// Hooks the Anvil engine's camera so each eye can render with its own pose/FOV.
// NOT IMPLEMENTED: addresses/signatures must be found by reverse engineering the game.
bool camera_install();
void camera_set_eye(const EyeView& view);   // apply this eye's offset before the game renders
void camera_restore();                      // restore original camera after both eyes
