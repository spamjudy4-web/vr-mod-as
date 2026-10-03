using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SlimeRancherVR
{
    [BepInPlugin("com.spamjudy4.slimerancher.vr", "Slime Rancher VR", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static Plugin Instance;
        internal static BepInEx.Logging.ManualLogSource Log;
        internal static ConfigEntry<float> RenderScale;
        internal static ConfigEntry<KeyCode> RecenterKey;
        internal static ConfigEntry<bool> MirrorToMonitor;
        internal static ConfigEntry<bool> HandAim;
        internal static ConfigEntry<KeyCode> HandAimKey;
        internal static ConfigEntry<float> AimPitchOffset;
        internal static ConfigEntry<float> SnapTurnDegrees;
        internal static ConfigEntry<bool> ShowControllers;

        private void Awake()
        {
            Instance = this;
            Log = Logger;
            RenderScale = Config.Bind("Rendering", "RenderScale", 1.0f,
                "Multiplier on the headset's recommended render resolution.");
            RecenterKey = Config.Bind("Input", "RecenterKey", KeyCode.F9,
                "Press to recenter the headset to the current view direction.");
            MirrorToMonitor = Config.Bind("Rendering", "MirrorToMonitor", true,
                "Draw the left-eye image on the desktop window.");

            HandAim = Config.Bind("Controls", "HandAim", true,
                "Aim with the right controller. When off, aim follows the mouse/gamepad and the controllers only send buttons.");
            HandAimKey = Config.Bind("Controls", "HandAimToggleKey", KeyCode.F10,
                "Toggle hand aiming at runtime (use if the aim loop misbehaves).");
            AimPitchOffset = Config.Bind("Controls", "AimPitchOffset", 0f,
                "Degrees to tilt the aim direction down from the controller's forward axis (Touch/Index grips point lower than they look).");
            SnapTurnDegrees = Config.Bind("Controls", "SnapTurnDegrees", 30f, "Snap-turn step for the right stick (hand aim mode).");
            ShowControllers = Config.Bind("Rendering", "ShowControllers", true, "Draw simple controller models and an aim line.");

            string error;
            if (!OpenVRSession.Init(out error))
            {
                Logger.LogError("VR disabled: " + error);
                enabled = false;
                return;
            }
            Logger.LogInfo("OpenVR initialised, starting VR rig.");
            gameObject.AddComponent<VRRig>();
        }

        private void OnDestroy()
        {
            OpenVRSession.Shutdown();
        }
    }
}
