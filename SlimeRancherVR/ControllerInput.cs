using System.Collections.Generic;
using BepInEx.Configuration;
using UnityEngine;
using Valve.VR;

namespace SlimeRancherVR
{
    /// <summary>
    /// Reads the VR controllers and turns them into keyboard/mouse input for the game.
    /// Left stick = WASD, right stick left/right = snap turn, right stick up/down = mouse wheel
    /// (hotbar). All buttons are remappable in the config file; the defaults are guesses at the
    /// game's default bindings.
    /// </summary>
    internal class ControllerInput
    {
        private class Binding
        {
            public string Source;
            public ConfigEntry<string> Output;
            public bool Down;
            public bool IsMouse; public NativeInput.MouseButton Mouse; public ushort Key;
        }

        private readonly List<Binding> bindings = new List<Binding>();
        private readonly bool[] stickKeys = new bool[4]; // W A S D
        private static readonly ushort[] StickVk = { 'W', 'A', 'S', 'D' };
        private bool turnLatched, wheelLatched;

        public uint LeftDevice = OpenVR.k_unTrackedDeviceIndexInvalid;
        public uint RightDevice = OpenVR.k_unTrackedDeviceIndexInvalid;
        /// <summary>Snap-turn request in degrees this frame (0 if none).</summary>
        public float TurnRequest;

        public ControllerInput(ConfigFile cfg)
        {
            Add(cfg, "RightTrigger", "MouseLeft");
            Add(cfg, "RightGrip", "MouseRight");
            Add(cfg, "RightA", "Space");
            Add(cfg, "RightB", "F");
            Add(cfg, "RightStickClick", "None");
            Add(cfg, "LeftTrigger", "LeftShift");
            Add(cfg, "LeftGrip", "E");
            Add(cfg, "LeftA", "R");
            Add(cfg, "LeftB", "Tab");
            Add(cfg, "LeftStickClick", "None");
        }

        private void Add(ConfigFile cfg, string source, string defaultOutput)
        {
            var b = new Binding
            {
                Source = source,
                Output = cfg.Bind("Controls", source, defaultOutput,
                    "Output for this button: None, MouseLeft, MouseRight, MouseMiddle, a letter/digit (e.g. F, 1), " +
                    "Space, LeftShift, LeftControl, Tab, Return, Escape.")
            };
            Parse(b);
            bindings.Add(b);
        }

        private static void Parse(Binding b)
        {
            string s = b.Output.Value.Trim();
            b.Key = 0; b.IsMouse = false;
            switch (s.ToLowerInvariant())
            {
                case "none": case "": return;
                case "mouseleft": b.IsMouse = true; b.Mouse = NativeInput.MouseButton.Left; return;
                case "mouseright": b.IsMouse = true; b.Mouse = NativeInput.MouseButton.Right; return;
                case "mousemiddle": b.IsMouse = true; b.Mouse = NativeInput.MouseButton.Middle; return;
                case "space": b.Key = 0x20; return;
                case "leftshift": b.Key = 0xA0; return;
                case "leftcontrol": b.Key = 0xA2; return;
                case "tab": b.Key = 0x09; return;
                case "return": b.Key = 0x0D; return;
                case "escape": b.Key = 0x1B; return;
            }
            if (s.Length == 1) b.Key = (ushort)char.ToUpperInvariant(s[0]);
        }

        public void Update(bool inputActive)
        {
            var sys = OpenVRSession.System;
            LeftDevice = sys.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.LeftHand);
            RightDevice = sys.GetTrackedDeviceIndexForControllerRole(ETrackedControllerRole.RightHand);
            TurnRequest = 0;

            VRControllerState_t left = default(VRControllerState_t), right = default(VRControllerState_t);
            bool haveL = Read(LeftDevice, ref left);
            bool haveR = Read(RightDevice, ref right);
            if (!inputActive) { ReleaseAll(); return; }

            foreach (var b in bindings)
            {
                bool leftSide = b.Source.StartsWith("Left");
                bool down = leftSide ? haveL && Pressed(left, b.Source.Substring(4))
                                     : haveR && Pressed(right, b.Source.Substring(5));
                if (down != b.Down) { Apply(b, down); b.Down = down; }
            }

            // Left stick -> WASD
            float lx = haveL ? left.rAxis0.x : 0, ly = haveL ? left.rAxis0.y : 0;
            SetStick(0, ly > 0.4f); SetStick(1, lx < -0.4f); SetStick(2, ly < -0.4f); SetStick(3, lx > 0.4f);

            // Right stick: X = snap turn, Y = hotbar wheel (edge-triggered)
            float rx = haveR ? right.rAxis0.x : 0, ry = haveR ? right.rAxis0.y : 0;
            if (Mathf.Abs(rx) > 0.7f) { if (!turnLatched) { TurnRequest = Mathf.Sign(rx) * Plugin.SnapTurnDegrees.Value; turnLatched = true; } }
            else if (Mathf.Abs(rx) < 0.3f) turnLatched = false;
            if (Mathf.Abs(ry) > 0.7f) { if (!wheelLatched) { NativeInput.MouseWheel(ry > 0 ? 1 : -1); wheelLatched = true; } }
            else if (Mathf.Abs(ry) < 0.3f) wheelLatched = false;
        }

        private static bool Read(uint dev, ref VRControllerState_t state)
        {
            if (dev == OpenVR.k_unTrackedDeviceIndexInvalid) return false;
            return OpenVRSession.System.GetControllerState(dev, ref state,
                (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(VRControllerState_t)));
        }

        private static bool Pressed(VRControllerState_t s, string button)
        {
            switch (button)
            {
                case "Trigger": return s.rAxis1.x > 0.6f;
                case "Grip": return Bit(s, EVRButtonId.k_EButton_Grip);
                case "A": return Bit(s, EVRButtonId.k_EButton_A);
                case "B": return Bit(s, EVRButtonId.k_EButton_ApplicationMenu);
                case "StickClick": return Bit(s, EVRButtonId.k_EButton_SteamVR_Touchpad);
            }
            return false;
        }

        private static bool Bit(VRControllerState_t s, EVRButtonId id) { return (s.ulButtonPressed & (1UL << (int)id)) != 0; }

        private static void Apply(Binding b, bool down)
        {
            if (b.IsMouse) NativeInput.SetMouseButton(b.Mouse, down);
            else if (b.Key != 0) NativeInput.SetKey(b.Key, down);
        }

        private void SetStick(int i, bool down)
        {
            if (stickKeys[i] == down) return;
            stickKeys[i] = down;
            NativeInput.SetKey(StickVk[i], down);
        }

        /// <summary>Release everything we are holding (focus loss, menus, shutdown) to avoid stuck keys.</summary>
        public void ReleaseAll()
        {
            foreach (var b in bindings) if (b.Down) { Apply(b, false); b.Down = false; }
            for (int i = 0; i < 4; i++) SetStick(i, false);
        }
    }
}
