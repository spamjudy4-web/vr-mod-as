using System.Collections;
using UnityEngine;
using Valve.VR;

namespace SlimeRancherVR
{
    /// <summary>
    /// Renders the game's main camera once per eye into RenderTextures and submits them to the
    /// OpenVR compositor. The headset pose is applied on top of the game's own camera, so
    /// mouse/gamepad look and movement keep working; head tracking only adds to them.
    /// </summary>
    internal class VRRig : MonoBehaviour
    {
        private readonly Camera[] eyeCams = new Camera[2];
        private readonly RenderTexture[] eyeRTs = new RenderTexture[2];
        private readonly TrackedDevicePose_t[] poses = new TrackedDevicePose_t[OpenVR.k_unMaxTrackedDeviceCount];
        private readonly Vector3[] eyeOffset = new Vector3[2];
        private readonly Quaternion[] eyeRot = new Quaternion[2];

        private Camera source;
        private int gameMask;
        private CameraClearFlags gameClear;
        private bool haveOrigin;
        private Vector3 originPos;
        private Quaternion originYaw = Quaternion.identity;
        private Vector3 headPos;
        private Quaternion headRot = Quaternion.identity;
        private bool rendered;

        private void Start()
        {
            uint w = 0, h = 0;
            OpenVRSession.System.GetRecommendedRenderTargetSize(ref w, ref h);
            int rw = Mathf.RoundToInt(w * Plugin.RenderScale.Value);
            int rh = Mathf.RoundToInt(h * Plugin.RenderScale.Value);

            for (int i = 0; i < 2; i++)
            {
                eyeRTs[i] = new RenderTexture(rw, rh, 24, RenderTextureFormat.ARGB32);
                eyeRTs[i].Create();

                var go = new GameObject("VR Eye " + i);
                DontDestroyOnLoad(go);
                eyeCams[i] = go.AddComponent<Camera>();
                eyeCams[i].enabled = false; // rendered manually via Render()

                var eye = i == 0 ? EVREye.Eye_Left : EVREye.Eye_Right;
                OpenVRSession.ToUnity(OpenVRSession.System.GetEyeToHeadTransform(eye),
                    out eyeOffset[i], out eyeRot[i]);
            }
            StartCoroutine(SubmitLoop());
        }

        private void LateUpdate()
        {
            rendered = false;
            if (source == null || !source.isActiveAndEnabled)
            {
                source = Camera.main;
                if (source == null) return;
                // The monitor shows the mirrored eye image; skip the redundant flat render.
                gameMask = source.cullingMask;
                gameClear = source.clearFlags;
                source.cullingMask = 0;
                source.clearFlags = CameraClearFlags.Nothing;
            }

            // Blocks until the compositor wants a new frame. Turn off the game's V-Sync.
            OpenVRSession.Compositor.WaitGetPoses(poses, null);
            var hmd = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];
            if (!hmd.bPoseIsValid) return;
            OpenVRSession.ToUnity(hmd.mDeviceToAbsoluteTracking, out headPos, out headRot);

            if (!haveOrigin || Input.GetKeyDown(Plugin.RecenterKey.Value)) Recenter();

            Quaternion invYaw = Quaternion.Inverse(originYaw);
            Vector3 relPos = invYaw * (headPos - originPos);
            Quaternion relRot = invYaw * headRot;

            for (int i = 0; i < 2; i++)
            {
                var cam = eyeCams[i];
                var eye = i == 0 ? EVREye.Eye_Left : EVREye.Eye_Right;

                cam.CopyFrom(source);
                cam.cullingMask = gameMask; // source's own mask is zeroed, so restore the saved one
                cam.clearFlags = gameClear;
                cam.targetTexture = eyeRTs[i];
                cam.enabled = false;
                cam.projectionMatrix = OpenVRSession.ToUnity(
                    OpenVRSession.System.GetProjectionMatrix(eye, source.nearClipPlane, source.farClipPlane));

                Transform t = cam.transform;
                Quaternion baseRot = source.transform.rotation;
                t.rotation = baseRot * relRot * eyeRot[i];
                t.position = source.transform.position + baseRot * (relPos + relRot * eyeOffset[i]);
                cam.Render();
            }
            rendered = true;
        }

        private void Recenter()
        {
            originPos = headPos;
            originYaw = Quaternion.Euler(0, headRot.eulerAngles.y, 0);
            haveOrigin = true;
        }

        private IEnumerator SubmitLoop()
        {
            var bounds = new VRTextureBounds_t { uMin = 0, uMax = 1, vMin = 0, vMax = 1 };
            var space = QualitySettings.activeColorSpace == ColorSpace.Linear ? EColorSpace.Linear : EColorSpace.Gamma;
            while (true)
            {
                yield return new WaitForEndOfFrame();
                if (!rendered) continue;
                for (int i = 0; i < 2; i++)
                {
                    var tex = new Texture_t
                    {
                        handle = eyeRTs[i].GetNativeTexturePtr(),
                        eType = ETextureType.DirectX,
                        eColorSpace = space
                    };
                    OpenVRSession.Compositor.Submit(i == 0 ? EVREye.Eye_Left : EVREye.Eye_Right,
                        ref tex, ref bounds, EVRSubmitFlags.Submit_Default);
                }
                OpenVRSession.Compositor.PostPresentHandoff();
            }
        }

        private void OnGUI()
        {
            if (Plugin.MirrorToMonitor.Value && eyeRTs[0] != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), eyeRTs[0], ScaleMode.ScaleAndCrop, false);
        }
    }
}
