using UnityEngine;
using ByteDance.PICO.XR;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace GravityRepulsion
{
    /// <summary>Four independently composed image modules; all cover lettering is generated art.</summary>
    public sealed class GravityTitleHud : MonoBehaviour
    {
        public static readonly Vector2 DesktopReferenceResolution = new(1600f, 1000f);
        public static readonly Vector2 PicoReferenceResolution = new(1600f, 1000f);
        private const float PicoPanelFallbackDistance = 2.2f;
        private const float PicoPanelCentreHeight = 2.08f;
        private const float PicoPanelScale = .002f;
        // Calibrated against the September 9 13:30 simulator reference. Its visible
        // framing differs from the raw XR eye viewport, so verify the actual window.
        private const float PicoHorizontalFill = .74f;
        private const float PicoPlacementDelay = 1.25f;
        private const float PicoCompositionLift = .045f;
        private Canvas canvas;
        private Camera boundCamera;
        private GravityTitleController controller;
        private RawImage title, art, start, exit;
        private LineRenderer[] rays = new LineRenderer[2];
        private Material rayMaterial;
        private bool[] held = new bool[2];
        private bool mouseHeld, touchHeld;
        private float nextDiagnostic;
        private int focus;
        private bool framingReported;
        private bool spatialPlacementLocked;
        private float spatialPanelDistance = PicoPanelFallbackDistance;
        private float spatialPlacementReadyAt;
        private Vector3 previousHeadPosition;
        private Quaternion previousHeadRotation;
        private float poseStableSince = -1f;
        private int lastPlacementFrame = -1;
        private readonly System.Collections.Generic.List<XRDisplaySubsystem> xrDisplays = new();
        private Matrix4x4 leftProjection, rightProjection;
        private Vector3 leftEyeOffset, rightEyeOffset;
        private bool renderPassProjection;
        private bool previousStartHover, previousExitHover;
        // 按钮悬停/按下动画状态
        private AudioSource menuAudio;
        private AudioClip hoverClip, clickClip;
        private float startHoverScale = 1f, exitHoverScale = 1f;
        private float startFlash = 0f, exitFlash = 0f;
        private static bool IsPico => Application.platform == RuntimePlatform.Android;
        private static bool PicoLayout => IsPico || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-gravityPicoMenuPreview") >= 0;
        public bool StartHovered { get; private set; }
        public bool ExitHovered { get; private set; }
        public bool IsSpatialReady => !PicoLayout || spatialPlacementLocked;

        private void Awake()
        {
            Configure();
            controller = FindFirstObjectByType<GravityTitleController>();
            // 加载开始菜单 UI 音效（来自 ange-embed ui_audio 集合）
            menuAudio = gameObject.AddComponent<AudioSource>();
            menuAudio.playOnAwake = false;
            menuAudio.spatialBlend = 0f;
            menuAudio.volume = 1f;
            hoverClip = Resources.Load<AudioClip>("Audio/UI_Hover_Scifi");
            clickClip = Resources.Load<AudioClip>("Audio/UI_Click_Scifi");
        }
        private void OnEnable() => Application.onBeforeRender += PlaceBeforeRender;
        private void OnDisable() => Application.onBeforeRender -= PlaceBeforeRender;
        public void ApplyLayoutForEditor() => Configure();
        private void LateUpdate()
        {
            if (Camera.main != boundCamera) Configure();
            if (!IsPico) PlaceBeforeCameraRender(boundCamera);
            FitBackground();
        }

        [BeforeRenderOrder(100)]
        private void PlaceBeforeRender() => PlaceBeforeCameraRender(boundCamera);

        // Stereo matrices/eye poses are authoritative while the XR camera renders.
        // LateUpdate can expose its desktop mirror projection instead.
        private void PlaceBeforeCameraRender(Camera camera)
        {
            if (!PicoLayout || spatialPlacementLocked || camera != boundCamera ||
                lastPlacementFrame == Time.frameCount || Time.unscaledTime < spatialPlacementReadyAt) return;
            lastPlacementFrame = Time.frameCount;
            if (IsPico && !RefreshRenderPassProjection()) return;
            var head = camera.transform;
            if (poseStableSince < 0f || Vector3.Distance(head.position, previousHeadPosition) > .025f ||
                Quaternion.Angle(head.rotation, previousHeadRotation) > 1.5f)
                poseStableSince = Time.unscaledTime;
            previousHeadPosition = head.position;
            previousHeadRotation = head.rotation;
            if (Time.unscaledTime - poseStableSince < .35f) return;
            FitSpatialPanelToInitialView();
            canvas.enabled = true;
            Canvas.ForceUpdateCanvases();
        }

        private bool RefreshRenderPassProjection()
        {
            SubsystemManager.GetSubsystems(xrDisplays);
            foreach (var display in xrDisplays)
            {
                if (!display.running) continue;
                int eyes = 0;
                for (int p = 0; p < display.GetRenderPassCount(); p++)
                {
                    display.GetRenderPass(p, out var pass);
                    for (int i = 0; i < pass.GetRenderParameterCount(); i++)
                    {
                        pass.GetRenderParameter(boundCamera, i, out var parameter);
                        var offset = boundCamera.transform.InverseTransformPoint(parameter.view.inverse.MultiplyPoint(Vector3.zero));
                        if (eyes == 0) { leftProjection = parameter.projection; leftEyeOffset = offset; }
                        else { rightProjection = parameter.projection; rightEyeOffset = offset; }
                        eyes++;
                        if (eyes == 2) { renderPassProjection = true; return true; }
                    }
                }
            }
            return false;
        }

        public void ReframeAtCurrentView()
        {
            if (!PicoLayout) return;
            spatialPlacementLocked = false;
            framingReported = false;
            poseStableSince = -1f;
            spatialPlacementReadyAt = Time.unscaledTime + PicoPlacementDelay;
            canvas.enabled = false;
        }

        private void Configure()
        {
            canvas = GetComponent<Canvas>(); boundCamera = Camera.main;
            if (canvas == null || boundCamera == null) return;
            canvas.worldCamera = boundCamera; canvas.overrideSorting = true; canvas.sortingOrder = 1000;
            var scaler = GetComponent<CanvasScaler>();
            var r = (RectTransform)transform;
            if (PicoLayout)
            {
                canvas.renderMode = RenderMode.WorldSpace;
                // The title is a real spatial panel anchored to the authored start,
                // not a head-locked overlay. Moving or turning the headset must reveal
                // that the panel remains at its original world position.
                var start = FindFirstObjectByType<GravityDesignStart>();
                var anchor = start != null ? start.transform : transform.parent;
                transform.SetParent(anchor, spatialPlacementLocked);
                r.sizeDelta = PicoReferenceResolution;
                if (!spatialPlacementLocked)
                {
                    r.localPosition = new Vector3(0f, PicoPanelCentreHeight, PicoPanelFallbackDistance);
                    r.localRotation = Quaternion.identity;
                }
                r.localScale = Vector3.one * PicoPanelScale;
                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize; scaler.referenceResolution = PicoReferenceResolution; }
                if (spatialPlacementReadyAt <= 0f)
                    spatialPlacementReadyAt = Application.isPlaying ? Time.unscaledTime + PicoPlacementDelay : 0f;
                canvas.enabled = !Application.isPlaying || spatialPlacementLocked;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.planeDistance = .4f;
                if (scaler != null) { scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = DesktopReferenceResolution; scaler.matchWidthOrHeight = .5f; }
            }
            foreach (Transform child in transform)
                if (child.name != "OPAQUE_TITLE_COVER" && !child.name.StartsWith("MODULE_")) child.gameObject.SetActive(false);
            float s = IsPico ? .92f : 1f;
            title = Module("MODULE_TITLE", "Title", new Vector2(-425f, 165f) * s, new Vector2(630f, 410f) * s);
            var oldArt = transform.Find("MODULE_KEY_ART"); if (oldArt != null) oldArt.gameObject.SetActive(false);
            art = Module("MODULE_BACKGROUND", "Background", Vector2.zero, DesktopReferenceResolution);
            art.transform.SetAsFirstSibling();
            art.material = Resources.Load<Material>("GravityUI/BackgroundFeather");
            var key = Resources.Load<Material>("GravityUI/TitleKey"); title.material = key;
            start = Module("MODULE_START", "Start", new Vector2(-425f, -130f) * s, new Vector2(490f, 165f) * s);
            exit = Module("MODULE_EXIT", "Exit", new Vector2(-425f, -300f) * s, new Vector2(450f, 145f) * s);
            start.material = key; exit.material = key;
            FitBackground();
            var cover = transform.Find("OPAQUE_TITLE_COVER");
            if (cover != null) { cover.SetAsFirstSibling(); cover.GetComponent<Image>().color = Color.black; cover.gameObject.SetActive(!PicoLayout); }
            Debug.Log("GRAVITY_MODULAR_COVER_READY language=English modules=background,title,start,exit generatedLettering=true");
        }

        private void FitBackground()
        {
            if (art == null || art.texture == null || boundCamera == null) return;
            var size = ((RectTransform)transform).rect.size;
            if (PicoLayout)
            {
                size = PicoReferenceResolution;
                art.rectTransform.anchoredPosition = Vector2.zero;
                var menuSize = PicoReferenceResolution * .9f;
                var centre = Vector2.up * (menuSize.y * PicoCompositionLift);
                LayoutInEye(title, centre, menuSize, new Vector2(-.24f,.16f), new Vector2(.42f,.29f));
                LayoutInEye(start, centre, menuSize, new Vector2(-.24f,-.10f), new Vector2(.35f,.14f));
                LayoutInEye(exit, centre, menuSize, new Vector2(-.24f,-.23f), new Vector2(.32f,.12f));
                if (!framingReported && spatialPlacementLocked)
                {
                    framingReported = true;
                    Debug.Log($"GRAVITY_TITLE_FRAMING_READY mode=spatial_world_panel horizontalFit={PicoHorizontalFill:0.000} stereo={boundCamera.stereoEnabled} panelDistance={spatialPanelDistance:0.00} panelCentreHeight={PicoPanelCentreHeight:0.00} panelWorldSize={PicoReferenceResolution.x * PicoPanelScale:0.00} backgroundAspect={(float)art.texture.width/art.texture.height:0.000} compositionLift={PicoCompositionLift:0.000}");
                }
            }
            art.rectTransform.sizeDelta = size;
            float imageRatio = (float)art.texture.width / art.texture.height;
            float displayRatio = size.x / size.y;
            art.uvRect = displayRatio > imageRatio
                ? new Rect(0f, (1f-imageRatio/displayRatio)*.5f, 1f, imageRatio/displayRatio)
                : new Rect((1f-displayRatio/imageRatio)*.5f, 0f, displayRatio/imageRatio, 1f);
            if (art.material != null) art.material.SetVector("_UvRect", new Vector4(art.uvRect.x, art.uvRect.y, art.uvRect.width, art.uvRect.height));
        }
        private Rect EyePlaneRect(Camera.StereoscopicEye eye, float distance)
        {
            bool stereo = boundCamera.stereoEnabled;
            var projection = renderPassProjection ? (eye == Camera.StereoscopicEye.Left ? leftProjection : rightProjection) : boundCamera.projectionMatrix;
            Vector3 eyeOffset = renderPassProjection ? (eye == Camera.StereoscopicEye.Left ? leftEyeOffset : rightEyeOffset) : Vector3.zero;
            float depth = distance - eyeOffset.z;
            return Rect.MinMaxRect(eyeOffset.x + (-1f + projection.m02) * depth / projection.m00,
                eyeOffset.y + (-1f + projection.m12) * depth / projection.m11,
                eyeOffset.x + (1f + projection.m02) * depth / projection.m00,
                eyeOffset.y + (1f + projection.m12) * depth / projection.m11);
        }
        private float VisibleWidthAtDistance(float distance)
        {
            var left = EyePlaneRect(Camera.StereoscopicEye.Left, distance);
            var right = EyePlaneRect(Camera.StereoscopicEye.Right, distance);
            return Mathf.Min(left.xMax, right.xMax) - Mathf.Max(left.xMin, right.xMin);
        }
        private void FitSpatialPanelToInitialView()
        {
            if (boundCamera == null) return;
            float targetViewWidth = PicoReferenceResolution.x * PicoPanelScale / PicoHorizontalFill;
            float low = .55f, high = PicoPanelFallbackDistance;
            while (VisibleWidthAtDistance(high) < targetViewWidth && high < 8f) high *= 1.5f;
            for (int i = 0; i < 20; i++)
            {
                float mid = (low + high) * .5f;
                if (VisibleWidthAtDistance(mid) < targetViewWidth) low = mid; else high = mid;
            }
            spatialPanelDistance = (low + high) * .5f;
            var rect = (RectTransform)transform;
            var left = EyePlaneRect(Camera.StereoscopicEye.Left, spatialPanelDistance);
            var right = EyePlaneRect(Camera.StereoscopicEye.Right, spatialPanelDistance);
            var safe = Rect.MinMaxRect(Mathf.Max(left.xMin, right.xMin), Mathf.Max(left.yMin, right.yMin),
                Mathf.Min(left.xMax, right.xMax), Mathf.Min(left.yMax, right.yMax));
            // Match the reference's slightly below-centre landscape panel. The
            // complete pose is captured once; normal head motion never moves it.
            var centre = new Vector3(safe.center.x, Mathf.Lerp(safe.yMin, safe.yMax, .49f), spatialPanelDistance);
            var worldPosition = boundCamera.transform.TransformPoint(centre);
            var anchor = FindFirstObjectByType<GravityDesignStart>()?.transform;
            if (anchor != null) rect.SetParent(anchor, true);
            rect.SetPositionAndRotation(worldPosition, boundCamera.transform.rotation);
            spatialPlacementLocked = true;
            Debug.Log($"GRAVITY_SPATIAL_PLACEMENT_COMPLETE renderPass={renderPassProjection} head={boundCamera.transform.position:F3} headRotation={boundCamera.transform.eulerAngles:F2} panel={rect.position:F3} distance={spatialPanelDistance:F3} sharedEyeRect={safe} leftProjection={leftProjection} rightProjection={rightProjection}");
            var corners = new Vector3[4]; rect.GetWorldCorners(corners);
            foreach (var eye in new[] { Camera.MonoOrStereoscopicEye.Left, Camera.MonoOrStereoscopicEye.Right })
                Debug.Log($"GRAVITY_TITLE_FINAL_BOUNDS eye={eye} bottomLeft={boundCamera.WorldToViewportPoint(corners[0], eye):F3} topRight={boundCamera.WorldToViewportPoint(corners[2], eye):F3}");
        }
        private static void LayoutInEye(RawImage item, Vector2 centre, Vector2 view, Vector2 position, Vector2 fraction)
        {
            if (item == null || item.texture == null) return;
            var size = Vector2.Scale(view, fraction);
            float ratio = (float)item.texture.width / item.texture.height;
            if(size.x/size.y > ratio) size.x=size.y*ratio; else size.y=size.x/ratio;
            item.rectTransform.anchoredPosition = centre + Vector2.Scale(view,position);
            item.rectTransform.sizeDelta = size;
        }
        private RawImage Module(string name, string resource, Vector2 pos, Vector2 size)
        {
            var t = transform.Find(name);
            if (t == null) { t = new GameObject(name, typeof(RectTransform), typeof(RawImage)).transform; t.SetParent(transform, false); }
            var image = t.GetComponent<RawImage>(); image.texture = Resources.Load<Texture2D>("GravityUI/" + resource);
            image.raycastTarget = false; image.color = Color.white;
            var r = image.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f, .5f);
            if (image.texture != null) { float ratio = (float)image.texture.width / image.texture.height; if (size.x / size.y > ratio) size.x = size.y * ratio; else size.y = size.x / ratio; }
            r.anchoredPosition = pos; r.sizeDelta = size; t.gameObject.SetActive(true);
            return image;
        }

        private void Update()
        {
            if (canvas == null || boundCamera == null || start == null || exit == null) return;
            if (!IsSpatialReady) return;
            bool sh = false, eh = false, activateStart = false, activateExit = false;
            if (Mouse.current != null)
            {
                var m = Mouse.current; var p = m.position.ReadValue();
                sh = Contains(start.rectTransform, p); eh = Contains(exit.rectTransform, p);
                bool down = m.leftButton.isPressed;
                activateStart = down && !mouseHeld && sh; activateExit = down && !mouseHeld && eh;
                mouseHeld = down;
            }
            if (Touchscreen.current != null)
            {
                var touch = Touchscreen.current.primaryTouch;
                bool down = touch.press.isPressed;
                var p = touch.position.ReadValue();
                bool hs = down && Contains(start.rectTransform, p), he = down && Contains(exit.rectTransform, p);
                sh |= hs; eh |= he;
                activateStart |= down && !touchHeld && hs; activateExit |= down && !touchHeld && he;
                touchHeld = down;
            }
            if (!IsPico && Keyboard.current != null)
            {
                var k = Keyboard.current;
                if (k.tabKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) focus = focus == 1 ? 2 : 1;
                if (k.enterKey.wasPressedThisFrame || k.spaceKey.wasPressedThisFrame) { activateStart |= focus == 1; activateExit |= focus == 2; }
                sh |= focus == 1; eh |= focus == 2;
            }
            for (int i = 0; i < 2; i++)
            {
                bool valid = ControllerRay(i == 0 ? XRNode.LeftHand : XRNode.RightHand, out var ray, out var device);
                bool down = valid && Pressed(device);
                bool handAim = HandRay(i, out var aimRay, out bool pinch);
                if (handAim) { valid = true; ray = aimRay; down = pinch; }
                bool hs = valid && Hit(start.rectTransform, ray, out _);
                bool he = valid && Hit(exit.rectTransform, ray, out _);
                sh |= hs; eh |= he;
                activateStart |= down && !held[i] && hs; activateExit |= down && !held[i] && he;
                held[i] = down;
                DrawRay(i, valid, ray, hs || he);
                if (!handAim && valid && ((hs && !previousStartHover) || (he && !previousExitHover))) device.SendHapticImpulse(0, .15f, .02f);
            }
            // 悬停进入检测（用于播放音效，防重复）
            bool startEnter = sh && !previousStartHover;
            bool exitEnter = eh && !previousExitHover;
            StartHovered = sh; ExitHovered = eh; previousStartHover = sh; previousExitHover = eh;

            // 悬停缩放：缓动到 1.05（约 0.1s）；按下强制拉到 0.97
            if (activateStart) startHoverScale = 0.97f;
            if (activateExit)  exitHoverScale  = 0.97f;
            startHoverScale = Mathf.Lerp(startHoverScale, sh ? 1.05f : 1f, Time.unscaledDeltaTime * 14f);
            exitHoverScale  = Mathf.Lerp(exitHoverScale,  eh ? 1.05f : 1f, Time.unscaledDeltaTime * 14f);

            // 亮度短闪：按下时置 1，快速衰减到 0；颜色向暖白色插值
            startFlash = Mathf.Lerp(startFlash, 0f, Time.unscaledDeltaTime * 9f);
            exitFlash  = Mathf.Lerp(exitFlash,  0f, Time.unscaledDeltaTime * 9f);
            if (activateStart) startFlash = 1f;
            if (activateExit)  exitFlash  = 1f;

            var startBase = sh ? Color.white : new Color(.84f, .91f, .95f);
            var exitBase  = eh ? Color.white : new Color(.76f, .79f, .84f);
            start.color = Color.Lerp(startBase, new Color(1f, .96f, .82f), startFlash * .6f);
            exit.color  = Color.Lerp(exitBase,  new Color(1f, .96f, .82f), exitFlash  * .6f);
            start.rectTransform.localScale = Vector3.one * startHoverScale;
            exit.rectTransform.localScale  = Vector3.one * exitHoverScale;

            // 悬停进入：轻播音效（hover 已在控制器循环里发震动）
            if ((startEnter || exitEnter) && menuAudio != null && hoverClip != null)
                menuAudio.PlayOneShot(hoverClip, .65f);

            // 按下：播放点击音效 + 较强震动
            if (activateStart || activateExit)
            {
                if (menuAudio != null && clickClip != null)
                    menuAudio.PlayOneShot(clickClip, 1f);
                for (int j = 0; j < 2; j++)
                    if (ControllerRay(j == 0 ? XRNode.LeftHand : XRNode.RightHand, out _, out var hd))
                        hd.SendHapticImpulse(0, .4f, .04f);
            }
            if (IsPico && Time.unscaledTime >= nextDiagnostic)
            {
                nextDiagnostic = Time.unscaledTime + 4f;
                Debug.Log($"GRAVITY_MENU_INPUT startHover={sh} exitHover={eh} leftPress={held[0]} rightPress={held[1]} mouse={Mouse.current != null} touch={Touchscreen.current != null}");
            }
            if (activateStart || activateExit) Debug.Log($"GRAVITY_MENU_HIT_ACTIVATE start={activateStart} exit={activateExit} ready={controller != null && controller.IsReadyForButtonInput}");
            if (controller != null && controller.IsReadyForButtonInput)
            {
                if (activateExit) controller.ExitGame();
                else if (activateStart) controller.StartGame();
            }
        }

        private bool ControllerRay(XRNode node, out Ray ray, out UnityEngine.XR.InputDevice d)
        {
            d = InputDevices.GetDeviceAtXRNode(node); ray = default;
            if (!d.isValid || (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out bool tracked) && !tracked) ||
                !d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out Vector3 p) || !d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out Quaternion q)) return false;
            var origin = boundCamera.GetComponentInParent<XROrigin>();
            var tracking = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : boundCamera.transform.parent;
            var world = tracking != null ? tracking.TransformPoint(p) : p;
            var direction = tracking != null ? tracking.TransformDirection(q * Vector3.forward) : q * Vector3.forward;
            if (direction.sqrMagnitude < .5f) return false;
            ray = new Ray(world, direction); return true;
        }
        private bool HandRay(int index, out Ray ray, out bool pressed)
        {
            ray = default; pressed = false;
#if UNITY_ANDROID && !UNITY_EDITOR
            var state = new HandAimState();
            if (!PXR_HandTracking.GetAimState(index == 0 ? HandType.HandLeft : HandType.HandRight, ref state) ||
                (state.aimStatus & HandAimStatus.AimRayValid) == 0) return false;
            var origin = boundCamera.GetComponentInParent<XROrigin>();
            var tracking = origin != null && origin.CameraFloorOffsetObject != null ? origin.CameraFloorOffsetObject.transform : boundCamera.transform.parent;
            var p = state.aimRayPose.Position.ToVector3();
            var d = state.aimRayPose.Orientation.ToQuat() * Vector3.forward;
            ray = new Ray(tracking != null ? tracking.TransformPoint(p) : p, tracking != null ? tracking.TransformDirection(d) : d);
            pressed = (state.aimStatus & (HandAimStatus.AimIndexPinching | HandAimStatus.AimRayTouched)) != 0;
            return true;
#else
            return false;
#endif
        }
        private static bool Pressed(UnityEngine.XR.InputDevice d) =>
            (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var t) && t) || (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out var a) && a) ||
            (d.TryGetFeatureValue(UnityEngine.XR.CommonUsages.trigger, out float analog) && analog > .65f);
        private bool Contains(RectTransform r, Vector2 p) => RectTransformUtility.RectangleContainsScreenPoint(r, p, boundCamera);
        private static bool Hit(RectTransform r, Ray ray, out float distance)
        {
            distance = 0f; if (!new Plane(r.forward, r.position).Raycast(ray, out distance) || distance <= 0f) return false;
            var local = r.InverseTransformPoint(ray.GetPoint(distance)); return r.rect.Contains(new Vector2(local.x, local.y));
        }
        private void DrawRay(int index, bool visible, Ray ray, bool hover)
        {
            if (rays[index] == null && visible)
            {
                var go = new GameObject("MENU_POINTER_" + index); go.transform.SetParent(transform, false);
                var line = go.AddComponent<LineRenderer>(); line.useWorldSpace = true; line.positionCount = 2;
                rayMaterial ??= new Material(Shader.Find("Sprites/Default")); line.sharedMaterial = rayMaterial;
                line.startWidth = .0025f; line.endWidth = .001f; line.sortingOrder = 2000; rays[index] = line;
            }
            var r = rays[index]; if (r == null) return; r.enabled = visible; if (!visible) return;
            var plane = new Plane(transform.forward, transform.position);
            float length = plane.Raycast(ray, out float d) && d > 0f ? d : 3f;
            r.SetPosition(0, ray.origin); r.SetPosition(1, ray.GetPoint(Mathf.Min(length, 5f)));
            r.startColor = r.endColor = hover ? new Color(.4f,1f,.75f) : new Color(.15f,.75f,1f,.7f);
        }
        private void OnDestroy() { if (rayMaterial != null) Destroy(rayMaterial); }
    }
}
