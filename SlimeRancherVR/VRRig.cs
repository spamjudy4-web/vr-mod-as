using System.Collections;
using UnityEngine;
using Valve.VR;

namespace SlimeRancherVR
{
    /// <summary>
    /// Renders the game's main camera once per eye into RenderTextures and submits them to the
    /// OpenVR compositor. The headset pose is applied on top of a base orientation:
    ///  - hand-aim mode: a body yaw we own (snap-turn), while the game's camera is steered to the
    ///    right controller's direction (see <see cref="HandAim"/>);
    ///  - otherwise: the game's own camera rotation (mouse/gamepad look).
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

        private ControllerInput input;
        private HandAim aim;
        private bool handAimOn;
        private float bodyYaw;
        private Transform leftModel, rightModel;

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

            input = new ControllerInput(Plugin.Instance.Config);
            aim = new HandAim();
            handAimOn = Plugin.HandAim.Value;
            if (Plugin.ShowControllers.Value)
            {
                leftModel = MakeModel("VR Left Hand", false);
                rightModel = MakeModel("VR Right Hand", true);
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
                bodyYaw = HandAim.YawOf(source.transform.forward);
            }

            // Blocks until the compositor wants a new frame. Turn off the game's V-Sync.
            OpenVRSession.Compositor.WaitGetPoses(poses, null);
            var hmd = poses[OpenVR.k_unTrackedDeviceIndex_Hmd];
            if (!hmd.bPoseIsValid) return;
            OpenVRSession.ToUnity(hmd.mDeviceToAbsoluteTracking, out headPos, out headRot);

            if (!haveOrigin || Input.GetKeyDown(Plugin.RecenterKey.Value)) Recenter();
            if (Input.GetKeyDown(Plugin.HandAimKey.Value)) ToggleHandAim();

            bool focused = Application.isFocused;
            input.Update(focused);
            if (handAimOn) bodyYaw += input.TurnRequest;

            Quaternion invYaw = Quaternion.Inverse(originYaw);
            Vector3 relPos = invYaw * (headPos - originPos);
            Quaternion relRot = invYaw * headRot;
            Quaternion baseRot = handAimOn ? Quaternion.Euler(0, bodyYaw, 0) : source.transform.rotation;
            Vector3 basePos = source.transform.position;

            UpdateHand(input.LeftDevice, leftModel, baseRot, basePos, invYaw, false, focused);
            UpdateHand(input.RightDevice, rightModel, baseRot, basePos, invYaw, true, focused);

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
                t.rotation = baseRot * relRot * eyeRot[i];
                t.position = basePos + baseRot * (relPos + relRot * eyeOffset[i]);
                cam.Render();
            }
            rendered = true;
        }

        /// <summary>Position a controller model; for the right hand also drive hand aiming.</summary>
        private void UpdateHand(uint dev, Transform model, Quaternion baseRot, Vector3 basePos,
                                Quaternion invYaw, bool isAimHand, bool focused)
        {
            bool valid = dev != OpenVR.k_unTrackedDeviceIndexInvalid && dev < poses.Length
                         && poses[dev].bPoseIsValid && poses[dev].bDeviceIsConnected;
            if (model != null) model.gameObject.SetActive(valid);
            if (!valid) return;

            Vector3 pos; Quaternion rot;
            OpenVRSession.ToUnity(poses[dev].mDeviceToAbsoluteTracking, out pos, out rot);
            Quaternion worldRot = baseRot * (invYaw * rot);
            Vector3 worldPos = basePos + baseRot * (invYaw * (pos - originPos));
            if (model != null) { model.rotation = worldRot; model.position = worldPos; }

            // Only steer the camera while the game has the cursor locked (i.e. in gameplay, not menus).
            if (isAimHand && handAimOn && focused && Cursor.lockState == CursorLockMode.Locked)
                aim.Update(source, worldRot * Quaternion.Euler(Plugin.AimPitchOffset.Value, 0, 0) * Vector3.forward);
        }

        private void Recenter()
        {
            originPos = headPos;
            originYaw = Quaternion.Euler(0, headRot.eulerAngles.y, 0);
            haveOrigin = true;
        }

        private void ToggleHandAim()
        {
            handAimOn = !handAimOn;
            aim.Reset();
            if (handAimOn && source != null) bodyYaw = HandAim.YawOf(source.transform.forward);
            Plugin.Log.LogInfo("Hand aim " + (handAimOn ? "on" : "off"));
        }

        private static Transform MakeModel(string name, bool withPointer)
        {
            Shader shader = Shader.Find("Sprites/Default"); // always-included shader; others may be stripped
            if (shader == null) return null;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            DestroyImmediate(go.GetComponent<Collider>()); // must not interfere with game physics
            go.transform.localScale = new Vector3(0.04f, 0.04f, 0.12f);
            var mat = new Material(shader) { color = new Color(0.9f, 0.9f, 0.95f) };
            go.GetComponent<Renderer>().material = mat;
            DontDestroyOnLoad(go);

            if (withPointer)
            {
                var line = new GameObject("Aim line");
                line.transform.SetParent(go.transform, false);
                line.transform.localScale = new Vector3(1 / 0.04f, 1 / 0.04f, 1 / 0.12f); // undo parent scale
                var lr = line.AddComponent<LineRenderer>();
                lr.useWorldSpace = false;
                lr.positionCount = 2;
                lr.SetPosition(0, Vector3.zero);
                lr.SetPosition(1, new Vector3(0, 0, 6f));
                lr.startWidth = lr.endWidth = 0.004f;
                lr.material = new Material(shader) { color = new Color(0.3f, 0.9f, 1f, 0.8f) };
            }
            return go.transform;
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

        private void OnApplicationFocus(bool focused)
        {
            if (!focused && input != null) input.ReleaseAll();
        }

        private void OnDestroy()
        {
            if (input != null) input.ReleaseAll();
        }

        private void OnGUI()
        {
            if (Plugin.MirrorToMonitor.Value && eyeRTs[0] != null && Event.current.type == EventType.Repaint)
                GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), eyeRTs[0], ScaleMode.ScaleAndCrop, false);
        }
    }
}
