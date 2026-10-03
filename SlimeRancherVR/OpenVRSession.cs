using System;
using UnityEngine;
using Valve.VR;

namespace SlimeRancherVR
{
    /// <summary>Thin wrapper over OpenVR init/shutdown and the OpenVR-to-Unity coordinate conversion.</summary>
    internal static class OpenVRSession
    {
        public static CVRSystem System;
        public static CVRCompositor Compositor;

        public static bool Init(out string error)
        {
            error = null;
            try
            {
                if (!OpenVR.IsRuntimeInstalled()) { error = "No OpenVR runtime installed (install SteamVR)."; return false; }
                var err = EVRInitError.None;
                System = OpenVR.Init(ref err, EVRApplicationType.VRApplication_Scene);
                if (err != EVRInitError.None) { error = OpenVR.GetStringForHmdError(err); return false; }
                Compositor = OpenVR.Compositor;
                return Compositor != null;
            }
            catch (Exception e) // e.g. DllNotFoundException when openvr_api.dll is missing
            {
                error = e.GetType().Name + ": " + e.Message;
                return false;
            }
        }

        public static void Shutdown()
        {
            if (System != null) OpenVR.Shutdown();
            System = null;
            Compositor = null;
        }

        /// <summary>OpenVR is right-handed (-Z forward); Unity is left-handed. Flip Z via basis change S*M*S.</summary>
        public static void ToUnity(HmdMatrix34_t m, out Vector3 pos, out Quaternion rot)
        {
            var a = new Matrix4x4();
            a.SetRow(0, new Vector4(m.m0, m.m1, m.m2, m.m3));
            a.SetRow(1, new Vector4(m.m4, m.m5, m.m6, m.m7));
            a.SetRow(2, new Vector4(m.m8, m.m9, m.m10, m.m11));
            a.SetRow(3, new Vector4(0, 0, 0, 1));
            var s = Matrix4x4.Scale(new Vector3(1, 1, -1));
            a = s * a * s;
            pos = a.GetColumn(3);
            rot = Quaternion.LookRotation(a.GetColumn(2), a.GetColumn(1));
        }

        public static Matrix4x4 ToUnity(HmdMatrix44_t m)
        {
            var a = new Matrix4x4();
            a.SetRow(0, new Vector4(m.m0, m.m1, m.m2, m.m3));
            a.SetRow(1, new Vector4(m.m4, m.m5, m.m6, m.m7));
            a.SetRow(2, new Vector4(m.m8, m.m9, m.m10, m.m11));
            a.SetRow(3, new Vector4(m.m12, m.m13, m.m14, m.m15));
            return a;
        }
    }
}
