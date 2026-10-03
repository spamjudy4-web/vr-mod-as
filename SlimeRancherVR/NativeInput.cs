using System;
using System.Runtime.InteropServices;

namespace SlimeRancherVR
{
    /// <summary>
    /// Injects keyboard/mouse input with SendInput. The game's input system is unknown, so this
    /// works at OS level and does not depend on which input library the game uses.
    /// </summary>
    internal static class NativeInput
    {
        public enum MouseButton { Left, Right, Middle }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOUSEINPUT { public int dx, dy; public uint mouseData, dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Sequential)]
        private struct KEYBDINPUT { public ushort wVk, wScan; public uint dwFlags, time; public IntPtr dwExtraInfo; }

        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MOUSEINPUT mi;
            [FieldOffset(0)] public KEYBDINPUT ki;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct INPUT { public uint type; public InputUnion u; }

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint n, INPUT[] inputs, int size);
        [DllImport("user32.dll")]
        private static extern uint MapVirtualKey(uint code, uint mapType);

        private const uint MOVE = 0x1, LDOWN = 0x2, LUP = 0x4, RDOWN = 0x8, RUP = 0x10,
                           MDOWN = 0x20, MUP = 0x40, WHEEL = 0x800;
        private const uint KEYUP = 0x2, SCANCODE = 0x8;

        private static void Send(INPUT i) { SendInput(1, new[] { i }, Marshal.SizeOf(typeof(INPUT))); }

        private static void Mouse(uint flags, int dx = 0, int dy = 0, int data = 0)
        {
            var i = new INPUT { type = 0 };
            i.u.mi = new MOUSEINPUT { dx = dx, dy = dy, mouseData = (uint)data, dwFlags = flags };
            Send(i);
        }

        public static void MouseMove(int dx, int dy) { if (dx != 0 || dy != 0) Mouse(MOVE, dx, dy); }
        public static void MouseWheel(int notches) { Mouse(WHEEL, 0, 0, notches * 120); }

        public static void SetMouseButton(MouseButton b, bool down)
        {
            switch (b)
            {
                case MouseButton.Left: Mouse(down ? LDOWN : LUP); break;
                case MouseButton.Right: Mouse(down ? RDOWN : RUP); break;
                default: Mouse(down ? MDOWN : MUP); break;
            }
        }

        /// <summary>Sends the key by scan code, which is picked up by DirectInput/raw-input games too.</summary>
        public static void SetKey(ushort vk, bool down)
        {
            var i = new INPUT { type = 1 };
            i.u.ki = new KEYBDINPUT
            {
                wScan = (ushort)MapVirtualKey(vk, 0),
                dwFlags = SCANCODE | (down ? 0u : KEYUP)
            };
            Send(i);
        }
    }
}
