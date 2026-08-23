using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    public sealed class GravityHud : MonoBehaviour
    {
        public Text waveText;
        public Text scoreText;
        public Text enemyText;
        public Text shieldText;
        public Text energyText;
        public Text repulseText;
        public Text leftText;
        public Text rightText;
        public Text statusText;
        public Image shieldFill;
        public Image energyFill;
        public Image repulseFill;
        public GameObject defeatPanel;
        public Text defeatText;

        private Coroutine statusRoutine;
        private Font uiFont;
        private Canvas rootCanvas;
        private RectTransform resultLeftCard;
        private RectTransform resultRightCard;
        private Text resultLeftText;
        private Text resultRightText;
        public bool EdgeLayoutConfigured { get; private set; }
        // The gameplay HUD is intentionally camera-space. The battlefield, targets and
        // controller rays remain in world space; this is only the small, always-readable
        // combat readout. Keeping it in ScreenSpaceCamera prevents a tracked-head pose
        // from exposing the reverse side of a world canvas (which mirrored the text).
        public static readonly Vector2 DesktopReferenceResolution = new(1600f, 1000f);
        // PICO submits one eye at a time.  A 2400-wide screen-space canvas was therefore
        // interpreted as a double-eye surface by the emulator: the left cluster fell beyond
        // the left eye and the right cluster past the right eye.  The PICO HUD is a single,
        // camera-relative world canvas with an explicitly safe per-eye layout instead.
        public static readonly Vector2 PicoReferenceResolution = new(2400f, 800f);
        private const float DesktopPlaneDistance = 1.05f;
        private const float PicoHudDistance = 2.20f;
        // Larger than desktop pixels by design: readable at normal PICO viewing distance,
        // while the wide canvas keeps the two clusters at the safe left/right edges.
        private const float PicoHudMetersPerPixel = .00175f;

        public bool CameraHudConfigured
        {
            get
            {
                rootCanvas ??= GetComponent<Canvas>();
                var scaler = GetComponent<CanvasScaler>();
                if (IsPicoRuntime)
                {
                    var camera = Camera.main;
                    return rootCanvas != null
                           && rootCanvas.renderMode == RenderMode.WorldSpace
                           && rootCanvas.worldCamera == camera
                           && camera != null
                           && transform.parent == camera.transform
                           && (transform as RectTransform) != null
                           && Vector3.Distance(transform.localPosition, new Vector3(0f, 0f, PicoHudDistance)) < .01f
                           && Mathf.Abs(transform.localScale.x - PicoHudMetersPerPixel) < .0001f
                           && scaler != null
                           && scaler.uiScaleMode == CanvasScaler.ScaleMode.ConstantPixelSize;
                }
                return rootCanvas != null
                       && rootCanvas.renderMode == RenderMode.ScreenSpaceCamera
                       && rootCanvas.worldCamera != null
                       && Mathf.Abs(rootCanvas.planeDistance - ActivePlaneDistance) < .01f
                       && scaler != null
                       && scaler.uiScaleMode == CanvasScaler.ScaleMode.ScaleWithScreenSize
                       && scaler.referenceResolution == ActiveReferenceResolution;
            }
        }
        public bool ResultLayoutConfigured => resultLeftCard != null && resultRightCard != null && resultLeftText != null && resultRightText != null;
        public bool ResultPanelVisible => defeatPanel != null && defeatPanel.activeSelf;
        public bool TopmostLayerConfigured => CameraHudConfigured;
        public string ResultTextContent => (resultLeftText != null ? resultLeftText.text : string.Empty) + "\n" + (resultRightText != null ? resultRightText.text : string.Empty);
        public Vector2 ActiveReferenceResolution => IsPicoRuntime ? PicoReferenceResolution : DesktopReferenceResolution;
        public float ActivePlaneDistance => IsPicoRuntime ? PicoHudDistance : DesktopPlaneDistance;
        public string ActiveSizeProfile => IsPicoRuntime ? "PICO_WorldSpace_1200x800" : "Desktop_1600x1000";
        private static bool IsPicoRuntime => Application.platform == RuntimePlatform.Android;

        private void Awake()
        {
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "PingFang SC", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" }, 28);
            EnsureResultCards();
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = uiFont;
            ConfigureCameraHud();
            HideRunSummary();
        }

        // Called by the scene upgrader so the serialized Unity scene shows the same
        // edge-only arrangement before entering Play mode.
        public void ApplyEdgeLayoutForEditor()
        {
            ConfigureCameraHud();
        }

        private void LateUpdate() => EnsureCameraHud();

        public void Refresh(GravityGameManager game)
        {
            if (game == null) return;
            waveText.text = $"威胁波次  {game.Wave:00}";
            scoreText.text = $"得分  {game.Score:00000}";
            enemyText.text = $"{game.AliveEnemies.Count:00} HOSTILES";
            shieldText.text = $"WARD  {Mathf.CeilToInt(game.Shield):000}";
            energyText.text = $"奇点能量  {Mathf.FloorToInt(game.UltimateCharge)}/{game.UltimateRequirement}";
            repulseText.text = game.RepulseCooldown > 0f ? $"斥力冷却 {game.RepulseCooldown:0.0}s" : "斥力就绪  F / 左主按钮";
            leftText.text = "L  " + (game.leftHand?.Held != null ? game.leftHand.Held.displayName : "未锁定");
            rightText.text = "R  " + (game.rightHand?.Held != null ? game.rightHand.Held.displayName : "未锁定");
            shieldFill.fillAmount = game.Shield / 100f;
            energyFill.fillAmount = game.UltimateCharge / game.UltimateRequirement;
            repulseFill.fillAmount = 1f - game.RepulseCooldown / game.repulseCooldownDuration;
        }

        public void SetStatus(string message)
        {
            if (statusText == null) return;
            if (statusRoutine != null) StopCoroutine(statusRoutine);
            statusRoutine = StartCoroutine(ShowStatusBriefly(message));
        }

        public void ShowRunSummary(GravityRunSummary summary)
        {
            if (defeatPanel == null) return;
            EnsureResultCards();
            EnsureCameraHud();
            defeatPanel.transform.SetAsLastSibling();
            defeatPanel.SetActive(true);
            EnsureResultCards();
            resultLeftText.text =
                $"<size=28><color=#FFD16A>本轮结算</color></size>\n\n" +
                $"<size=32><color=#FFFFFF>总分  {summary.TotalScore:00000}</color></size>\n\n" +
                $"通过关数  {summary.completedWaves:00}\n" +
                $"击破目标  {summary.enemiesDefeated:00}\n\n" +
                $"基础战果  +{summary.baseCombatScore}\n" +
                $"波次守护  +{summary.waveScore}";
            resultRightText.text =
                $"<size=24><color=#78E8FF>额外奖励</color></size>\n\n" +
                $"精准防御 ×{summary.precisionDefenseCount}   +{summary.precisionDefenseBonus}\n" +
                $"反弹进攻 ×{summary.reflectAttackCount}   +{summary.reflectAttackBonus}\n" +
                $"斥力击破 ×{summary.repulseAttackCount}   +{summary.repulseAttackBonus}\n" +
                $"双极对撞 ×{summary.collisionAttackCount}   +{summary.collisionAttackBonus}\n" +
                $"环境连锁 ×{summary.environmentAttackCount}   +{summary.environmentAttackBonus}\n" +
                $"奇点湮灭 ×{summary.ultimateAttackCount}   +{summary.ultimateAttackBonus}";
            defeatText.text = "按 Enter / 右手 A 重新部署";
            Canvas.ForceUpdateCanvases();
        }

        public void HideRunSummary()
        {
            if (defeatPanel != null) defeatPanel.SetActive(false);
        }

        private void ConfigureCameraHud()
        {
            EnsureCameraHud();
            var backplate = transform.Find("HUD_Backplate_XRReadable");
            if (backplate != null)
            {
                // The combat readout deliberately has no full-screen or centre plate.
                // It is two small edge clusters, leaving the battlefield unobscured.
                backplate.gameObject.SetActive(false);
                var plate = backplate.GetComponent<Image>();
                if (plate != null) plate.color = Color.clear;
            }

            var layout = CurrentLayout;
            // Anchoring to the actual left/right canvas bounds keeps the two clusters at
            // the edge in the desktop view and inside each PICO eye's safe field of view.
            PlaceLeft(waveText, layout.EdgeMargin, layout.ScaleY(405f), layout.ScaleSize(new Vector2(330f, 44f)), TextAnchor.MiddleLeft, layout.ScaleFont(24));
            PlaceLeft(enemyText, layout.EdgeMargin, layout.ScaleY(352f), layout.ScaleSize(new Vector2(330f, 38f)), TextAnchor.MiddleLeft, layout.ScaleFont(21));
            PlaceLeft(leftText, layout.EdgeMargin, layout.ScaleY(-275f), layout.ScaleSize(new Vector2(330f, 38f)), TextAnchor.MiddleLeft, layout.ScaleFont(19));
            PlaceLeft(repulseText, layout.EdgeMargin, layout.ScaleY(-329f), layout.ScaleSize(new Vector2(330f, 34f)), TextAnchor.MiddleLeft, layout.ScaleFont(18));
            PlaceTrackLeft(repulseFill, layout.EdgeMargin, layout.ScaleY(-359f), layout.ScaleSize(new Vector2(310f, 10f)));

            PlaceRight(scoreText, layout.EdgeMargin, layout.ScaleY(405f), layout.ScaleSize(new Vector2(330f, 44f)), TextAnchor.MiddleRight, layout.ScaleFont(24));
            PlaceRight(energyText, layout.EdgeMargin, layout.ScaleY(352f), layout.ScaleSize(new Vector2(330f, 38f)), TextAnchor.MiddleRight, layout.ScaleFont(21));
            PlaceTrackRight(energyFill, layout.EdgeMargin, layout.ScaleY(323f), layout.ScaleSize(new Vector2(310f, 10f)));
            PlaceRight(shieldText, layout.EdgeMargin, layout.ScaleY(-275f), layout.ScaleSize(new Vector2(330f, 34f)), TextAnchor.MiddleRight, layout.ScaleFont(18));
            PlaceTrackRight(shieldFill, layout.EdgeMargin, layout.ScaleY(-305f), layout.ScaleSize(new Vector2(310f, 10f)));
            PlaceRight(rightText, layout.EdgeMargin, layout.ScaleY(-365f), layout.ScaleSize(new Vector2(330f, 38f)), TextAnchor.MiddleRight, layout.ScaleFont(19));

            // Brief interaction feedback is also an edge element. It starts hidden and
            // never leaves a permanent message across the middle of the view.
            PlaceLeft(statusText, layout.EdgeMargin, layout.ScaleY(-425f), layout.ScaleSize(new Vector2(360f, 42f)), TextAnchor.LowerLeft, layout.ScaleFont(18));
            if (statusText != null) statusText.gameObject.SetActive(false);

            var help = transform.Find("Help")?.GetComponent<Text>();
            if (help != null) help.gameObject.SetActive(false);
            var reticle = transform.Find("Reticle");
            if (reticle != null) reticle.gameObject.SetActive(false);

            foreach (var label in GetComponentsInChildren<Text>(true)) label.raycastTarget = false;
            foreach (var image in GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;

            EdgeLayoutConfigured = CameraHudConfigured
                                  && waveText != null && scoreText != null
                                  && leftText != null && rightText != null
                                  && backplate != null && !backplate.gameObject.activeSelf;
            Debug.Log($"GRAVITY_EDGE_HUD_CONFIG canvas={rootCanvas?.renderMode} profile={ActiveSizeProfile} reference={ActiveReferenceResolution.x:0}x{ActiveReferenceResolution.y:0} distance={ActivePlaneDistance:0.00} camera={rootCanvas?.worldCamera?.name ?? "missing"} backplate=off");
        }

        public bool ValidateViewportLayout(Camera camera, out string failure)
        {
            failure = string.Empty;
            if (camera == null)
            {
                failure = "camera missing";
                return false;
            }
            if (!CameraHudConfigured) failure += "camera_hud_not_configured;";
            if (rootCanvas.worldCamera != camera) failure += "camera_hud_not_bound_to_active_camera;";
            var scaler = GetComponent<CanvasScaler>();
            if (scaler == null || scaler.referenceResolution != ActiveReferenceResolution)
                failure += $"unexpected_reference_resolution[{scaler?.referenceResolution.ToString() ?? "missing"}];";
            return failure.Length == 0;
        }

        public bool ValidateResultLayout(Camera camera, out string failure)
        {
            failure = string.Empty;
            if (camera == null || !ResultLayoutConfigured || defeatPanel == null || !defeatPanel.activeSelf)
            {
                failure = "result_panel_camera_or_cards_missing";
                return false;
            }
            Canvas.ForceUpdateCanvases();
            if (!CameraHudConfigured) failure += "result_canvas_not_camera_hud;";
            if (resultLeftCard.anchoredPosition.x > -90f || resultRightCard.anchoredPosition.x < 90f)
                failure += "result_cards_not_spatial_board_columns;";
            if (ResultTextContent.Contains("失败") || ResultTextContent.Contains("失守")) failure += "negative_failure_copy_present;";
            if (!ResultTextContent.Contains("总分") || !ResultTextContent.Contains("通过关数") || !ResultTextContent.Contains("精准防御") || !ResultTextContent.Contains("反弹进攻")) failure += "result_breakdown_incomplete;";
            return failure.Length == 0;
        }

        public bool ValidateHandAvoidance(Camera camera, GravityHand leftHand, GravityHand rightHand, out string failure)
        {
            failure = string.Empty;
            if (camera == null || leftHand == null || rightHand == null)
            {
                failure = "hand_or_camera_missing";
                return false;
            }
            if (!CameraHudConfigured) failure += "camera_hud_not_configured;";
            if (rootCanvas.worldCamera != camera) failure += "camera_hud_not_bound_to_active_camera;";
            return failure.Length == 0;
        }

        private IEnumerator ShowStatusBriefly(string message)
        {
            if (statusText == null) yield break;
            statusText.gameObject.SetActive(true);
            statusText.text = message;
            statusText.canvasRenderer.SetAlpha(1f);
            yield return new WaitForSeconds(1.65f);
            statusText.CrossFadeAlpha(0f, .42f, true);
            yield return new WaitForSeconds(.45f);
            statusText.text = string.Empty;
            statusText.gameObject.SetActive(false);
            statusRoutine = null;
        }

        private static IEnumerator HideHelpAfterDelay(Text help)
        {
            yield return new WaitForSeconds(6.5f);
            if (help == null) yield break;
            help.CrossFadeAlpha(0f, .5f, true);
            yield return new WaitForSeconds(.55f);
            if (help != null) help.gameObject.SetActive(false);
        }

        private void EnsureCameraHud()
        {
            rootCanvas ??= GetComponent<Canvas>();
            if (rootCanvas == null) return;
            var camera = Camera.main;
            if (IsPicoRuntime)
            {
                if (camera == null) return;
                rootCanvas.renderMode = RenderMode.WorldSpace;
                rootCanvas.overrideSorting = true;
                rootCanvas.sortingOrder = 20;
                rootCanvas.worldCamera = camera;
                var picoScaler = GetComponent<CanvasScaler>();
                if (picoScaler != null)
                {
                    picoScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    picoScaler.scaleFactor = 1f;
                    picoScaler.referencePixelsPerUnit = 100f;
                }
                if (transform.parent != camera.transform) transform.SetParent(camera.transform, false);
                if (transform is RectTransform picoRect)
                {
                    picoRect.anchorMin = picoRect.anchorMax = new Vector2(.5f, .5f);
                    picoRect.pivot = new Vector2(.5f, .5f);
                    picoRect.sizeDelta = PicoReferenceResolution;
                    picoRect.localPosition = new Vector3(0f, 0f, PicoHudDistance);
                    picoRect.localRotation = Quaternion.identity;
                    picoRect.localScale = Vector3.one * PicoHudMetersPerPixel;
                }
                return;
            }
            rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 20;
            rootCanvas.planeDistance = ActivePlaneDistance;
            rootCanvas.worldCamera = camera;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = ActiveReferenceResolution;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
                scaler.matchWidthOrHeight = .5f;
            }
            if (transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = ActiveReferenceResolution;
                rect.localScale = Vector3.one;
            }
        }

        private void EnsureResultCards()
        {
            if (defeatPanel == null) return;
            var panelRect = defeatPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.anchoredPosition = Vector2.zero;
            var layout = CurrentLayout;
            panelRect.sizeDelta = layout.ScaleSize(new Vector2(1120f, 630f));
            var panelImage = defeatPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.clear;
                panelImage.raycastTarget = false;
            }
            resultLeftCard = CreateResultCard("RESULT_LEFT_CARD_本轮结算", layout.Scale(new Vector2(-285f, 0f)), layout.ScaleSize(new Vector2(440f, 470f)), new Color(.025f, .045f, .09f, .92f), out resultLeftText);
            resultRightCard = CreateResultCard("RESULT_RIGHT_CARD_额外奖励", layout.Scale(new Vector2(285f, 0f)), layout.ScaleSize(new Vector2(440f, 470f)), new Color(.035f, .035f, .085f, .92f), out resultRightText);
            Place(defeatText, layout.Scale(new Vector2(0f, -288f)), layout.ScaleSize(new Vector2(760f, 42f)), TextAnchor.MiddleCenter, layout.ScaleFont(21));
            if (defeatText != null) defeatText.color = new Color(.86f, .93f, 1f, .95f);
            defeatPanel.transform.SetAsLastSibling();
        }

        private RectTransform CreateResultCard(string objectName, Vector2 position, Vector2 size, Color color, out Text text)
        {
            var existing = defeatPanel.transform.Find(objectName);
            var card = existing != null ? existing.gameObject : new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            card.transform.SetParent(defeatPanel.transform, false);
            var rect = card.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = card.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            var textTransform = card.transform.Find("Text");
            var textObject = textTransform != null ? textTransform.gameObject : new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            textObject.transform.SetParent(card.transform, false);
            text = textObject.GetComponent<Text>();
            text.font = uiFont;
            text.fontSize = 20;
            text.lineSpacing = 1.12f;
            text.alignment = TextAnchor.MiddleLeft;
            text.supportRichText = true;
            text.color = new Color(.87f, .95f, 1f, 1f);
            text.raycastTarget = false;
            var textRect = text.rectTransform;
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = new Vector2(34f, 28f);
            textRect.offsetMax = new Vector2(-34f, -28f);
            var outline = text.GetComponent<Outline>() ?? text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(.01f, .015f, .04f, .9f);
            outline.effectDistance = new Vector2(1f, -1f);
            return rect;
        }

        private HudLayout CurrentLayout => IsPicoRuntime ? HudLayout.Pico : HudLayout.Desktop;

        private readonly struct HudLayout
        {
            public static readonly HudLayout Desktop = new(34f, 1f, 1f, 1f);
            // These values are in the 2400x800 PICO world-canvas space.  The cluster
            // centres land about +/- 25 degrees from centre, and 1.75x text is readable
            // in the headset without becoming a centre-screen panel.
            public static readonly HudLayout Pico = new(360f, 1.25f, .78f, 1.75f);

            private readonly float edgeMargin;
            private readonly float horizontalScale;
            private readonly float verticalScale;
            private readonly float fontScale;

            public float EdgeMargin => edgeMargin;

            public HudLayout(float edgeMargin, float horizontalScale, float verticalScale, float fontScale)
            {
                this.edgeMargin = edgeMargin;
                this.horizontalScale = horizontalScale;
                this.verticalScale = verticalScale;
                this.fontScale = fontScale;
            }

            public Vector2 Scale(Vector2 value) => new(value.x * horizontalScale, value.y * verticalScale);
            public Vector2 ScaleSize(Vector2 value) => new(value.x * horizontalScale, value.y * verticalScale);
            public float ScaleY(float value) => value * verticalScale;
            public int ScaleFont(int value) => Mathf.RoundToInt(value * fontScale);
        }

        private static void Place(Text label, Vector2 position, Vector2 size, TextAnchor alignment, int fontSize)
        {
            if (label == null) return;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            label.alignment = alignment;
            label.fontSize = fontSize;
        }

        private static void PlaceTrack(Image fill, Vector2 position, Vector2 size)
        {
            if (fill == null || fill.transform.parent is not RectTransform track) return;
            track.anchorMin = track.anchorMax = new Vector2(.5f, .5f);
            track.pivot = new Vector2(.5f, .5f);
            track.anchoredPosition = position;
            track.sizeDelta = size;
        }

        private static void PlaceLeft(Text label, float margin, float y, Vector2 size, TextAnchor alignment, int fontSize)
        {
            if (label == null) return;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, .5f);
            rect.pivot = new Vector2(0f, .5f);
            rect.anchoredPosition = new Vector2(margin, y);
            rect.sizeDelta = size;
            label.alignment = alignment;
            label.fontSize = fontSize;
        }

        private static void PlaceRight(Text label, float margin, float y, Vector2 size, TextAnchor alignment, int fontSize)
        {
            if (label == null) return;
            var rect = label.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(1f, .5f);
            rect.pivot = new Vector2(1f, .5f);
            rect.anchoredPosition = new Vector2(-margin, y);
            rect.sizeDelta = size;
            label.alignment = alignment;
            label.fontSize = fontSize;
        }

        private static void PlaceTrackLeft(Image fill, float margin, float y, Vector2 size)
        {
            if (fill == null || fill.transform.parent is not RectTransform track) return;
            track.anchorMin = track.anchorMax = new Vector2(0f, .5f);
            track.pivot = new Vector2(0f, .5f);
            track.anchoredPosition = new Vector2(margin, y);
            track.sizeDelta = size;
        }

        private static void PlaceTrackRight(Image fill, float margin, float y, Vector2 size)
        {
            if (fill == null || fill.transform.parent is not RectTransform track) return;
            track.anchorMin = track.anchorMax = new Vector2(1f, .5f);
            track.pivot = new Vector2(1f, .5f);
            track.anchoredPosition = new Vector2(-margin, y);
            track.sizeDelta = size;
        }

        private static bool AtLeft(Graphic graphic, Camera camera, float threshold) => ViewportBounds(graphic, camera).xMin <= threshold;
        private static bool AtRight(Graphic graphic, Camera camera, float threshold) => ViewportBounds(graphic, camera).xMax >= threshold;
        private static bool AtTop(Graphic graphic, Camera camera, float threshold) => ViewportBounds(graphic, camera).yMax >= threshold;
        private static bool AtBottom(Graphic graphic, Camera camera, float threshold) => ViewportBounds(graphic, camera).yMin <= threshold;

        private static Rect ViewportBounds(Graphic graphic, Camera camera)
        {
            if (graphic == null) return new Rect(.5f, .5f, 0f, 0f);
            var corners = new Vector3[4];
            graphic.rectTransform.GetWorldCorners(corners);
            var min = Vector2.one * float.PositiveInfinity;
            var max = Vector2.one * float.NegativeInfinity;
            foreach (var corner in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(camera, corner);
                var normalized = new Vector2(screen.x / Mathf.Max(1f, Screen.width), screen.y / Mathf.Max(1f, Screen.height));
                min = Vector2.Min(min, normalized);
                max = Vector2.Max(max, normalized);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect NormalizedScreenRect(RectTransform rect)
        {
            if (rect == null) return new Rect(.5f, .5f, 0f, 0f);
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            var canvas = rect.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            var min = Vector2.one * float.PositiveInfinity;
            var max = Vector2.one * float.NegativeInfinity;
            foreach (var corner in corners)
            {
                var screen = RectTransformUtility.WorldToScreenPoint(eventCamera, corner);
                var normalized = new Vector2(screen.x / Mathf.Max(1f, Screen.width), screen.y / Mathf.Max(1f, Screen.height));
                min = Vector2.Min(min, normalized);
                max = Vector2.Max(max, normalized);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static Rect HandViewportRect(Camera camera, GravityHand hand)
        {
            var center = camera.WorldToViewportPoint(hand.transform.position);
            var half = new Vector2(.105f, .16f);
            return Rect.MinMaxRect(center.x - half.x, center.y - half.y, center.x + half.x, center.y + half.y);
        }
    }
}
