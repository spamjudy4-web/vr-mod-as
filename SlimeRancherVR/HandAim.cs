using UnityEngine;

namespace SlimeRancherVR
{
    /// <summary>
    /// Makes the game aim where the right controller points, without knowing how the game reads its
    /// camera: a closed loop that sends relative mouse movement until the game camera's yaw/pitch
    /// match the hand's. The mouse-counts-per-degree gain is learned on the fly, so mouse sensitivity
    /// and pointer acceleration don't need to be known. Sign is learned from divergence.
    /// </summary>
    internal class HandAim
    {
        private class Axis
        {
            public float K;            // degrees of camera rotation per mouse count (signed)
            public float Sent;         // counts sent last frame
            public float Remainder;    // fractional counts carried over
            public float PrevCurrent;  // camera angle last frame
            public int Wrong;          // consecutive frames the camera moved opposite to expectation
        }

        private readonly Axis yaw = new Axis { K = 0.15f };
        private readonly Axis pitch = new Axis { K = -0.15f }; // mouse down (+dy) looks down
        private const float Gain = 0.4f;
        private const int MaxCounts = 150;

        public static float YawOf(Vector3 d) { return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }
        public static float PitchOf(Vector3 d) { return Mathf.Asin(Mathf.Clamp(d.y, -1f, 1f)) * Mathf.Rad2Deg; }

        public void Update(Camera source, Vector3 aimDir)
        {
            Vector3 f = source.transform.forward;
            float dx = Step(yaw, YawOf(f), YawOf(aimDir), true);
            float dy = Step(pitch, PitchOf(f), PitchOf(aimDir), false);
            NativeInput.MouseMove(Mathf.RoundToInt(dx), Mathf.RoundToInt(dy));
        }

        /// <summary>Returns the number of mouse counts to send this frame for one axis.</summary>
        private static float Step(Axis a, float current, float target, bool wrap)
        {
            float error = wrap ? Mathf.DeltaAngle(current, target) : target - current;

            // Learn how far the camera moved per count sent last frame. This is independent of hand
            // motion, so it also tells us if the sign is wrong (camera consistently moves the wrong way).
            if (Mathf.Abs(a.Sent) >= 10f)
            {
                float moved = wrap ? Mathf.DeltaAngle(a.PrevCurrent, current) : current - a.PrevCurrent;
                float k = moved / a.Sent;
                bool sameSign = Mathf.Sign(k) == Mathf.Sign(a.K);
                if (sameSign && Mathf.Abs(k) > 0.005f && Mathf.Abs(k) < 2f) { a.K = Mathf.Lerp(a.K, k, 0.1f); a.Wrong = 0; }
                else if (!sameSign && Mathf.Abs(moved) > 0.2f) a.Wrong++;
                if (a.Wrong >= 8) { a.K = -a.K; a.Wrong = 0; a.Remainder = 0; }
            }

            a.PrevCurrent = current;

            if (Mathf.Abs(error) < 0.3f) { a.Sent = 0; a.Remainder = 0; return 0; } // dead zone

            float counts = Mathf.Clamp(error * Gain / a.K + a.Remainder, -MaxCounts, MaxCounts);
            float whole = Mathf.Round(counts);
            a.Remainder = counts - whole;
            a.Sent = whole;
            return whole;
        }

        public void Reset()
        {
            yaw.Sent = pitch.Sent = 0; yaw.Remainder = pitch.Remainder = 0;
        }
    }
}
