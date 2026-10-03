using BepInEx;
using BepInEx.Configuration;
using UnityEngine;

namespace SlimeRancherVR
{
    [BepInPlugin("com.spamjudy4.slimerancher.vr", "Slime Rancher VR", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        internal static ConfigEntry<float> RenderScale;
        internal static ConfigEntry<KeyCode> RecenterKey;
        internal static ConfigEntry<bool> MirrorToMonitor;

        private void Awake()
        {
            RenderScale = Config.Bind("Rendering", "RenderScale", 1.0f,
                "Multiplier on the headset's recommended render resolution.");
            RecenterKey = Config.Bind("Input", "RecenterKey", KeyCode.F9,
                "Press to recenter the headset to the current view direction.");
            MirrorToMonitor = Config.Bind("Rendering", "MirrorToMonitor", true,
                "Draw the left-eye image on the desktop window.");

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
