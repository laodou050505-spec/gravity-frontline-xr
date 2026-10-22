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
        private GravityGeneratedHud generated;
        private GravityVisorHud visor;
        private GravityVisorDamage visorDamage;
        private Font uiFont;
        private Canvas rootCanvas;
        private RectTransform resultLeftCard;
        private RectTransform resultRightCard;
        private Text resultLeftText;
        private Text resultRightText;
        private RectTransform topLeftFrame;
        private RectTransform topRightFrame;
        private RectTransform bottomLeftFrame;
        private RectTransform bottomRightFrame;
        private Color shieldBaseColor = Color.white;
        private Color energyBaseColor = Color.white;
        private Color repulseBaseColor = Color.white;
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
        public static readonly Vector2 PicoReferenceResolution = new(2400f, 2400f);
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
        public bool TopmostLayerConfigured => CameraHudConfigured && visor != null && visor.TopmostConfigured;
        public string ResultTextContent => (resultLeftText != null ? resultLeftText.text : string.Empty) + "\n" + (resultRightText != null ? resultRightText.text : string.Empty);
        public Vector2 ActiveReferenceResolution => IsPicoRuntime ? PicoReferenceResolution : DesktopReferenceResolution;
        public float ActivePlaneDistance => IsPicoRuntime ? PicoHudDistance : DesktopPlaneDistance;
        public string ActiveSizeProfile => IsPicoRuntime ? "PICO_HeadRelative_2400x2400" : "Desktop_1600x1000";
        public static bool UsesPicoLayout => Application.platform == RuntimePlatform.Android || System.Array.IndexOf(System.Environment.GetCommandLineArgs(), "-gravityPicoHudPreview") >= 0;
        private static bool IsPicoRuntime => UsesPicoLayout;

        private void Awake()
        {
            // 优先加载嵌入字体（Arimo，Apache 2.0），确保 PICO 设备上与桌面一致
            var arimoAsset = Resources.Load<Font>("GravityFonts/Arimo-Regular");
            uiFont = arimoAsset != null
                ? arimoAsset
                : Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica" }, 28);
            EnsureResultCards();
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = uiFont;
            shieldBaseColor = shieldText != null ? shieldText.color : Color.white;
            energyBaseColor = energyText != null ? energyText.color : Color.white;
            repulseBaseColor = repulseText != null ? repulseText.color : Color.white;
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
            visorDamage?.SetHealth(game.Shield);
            if (visor != null && visor.Ready)
            {
                visor.Refresh(game);
                ApplyReadoutFeedback(game);
                return;
            }
            if (generated != null && generated.Ready)
            {
                generated.Refresh(game);
                ApplyReadoutFeedback(game);
                return;
            }
            waveText.text = $"WAVE  {game.Wave:00}";
            scoreText.text = $"SCORE  {game.Score:00000}";
            enemyText.text = $"{game.AliveEnemies.Count:00} HOSTILES";
            shieldText.text = $"SHIELD  {Mathf.CeilToInt(game.Shield):000}";
            energyText.text = $"SINGULARITY  {Mathf.FloorToInt(game.UltimateCharge)}/{game.UltimateRequirement}";
            repulseText.text = game.RepulseCooldown > 0f ? $"REPULSE COOLDOWN {game.RepulseCooldown:0.0}s" : "REPULSE READY  [Press Left X]";
            leftText.text = HandReadout("L", game.leftHand);
            rightText.text = HandReadout("R", game.rightHand);
            shieldFill.fillAmount = game.Shield / 100f;
            energyFill.fillAmount = game.UltimateCharge / game.UltimateRequirement;
            repulseFill.fillAmount = 1f - game.RepulseCooldown / game.repulseCooldownDuration;
            ApplyReadoutFeedback(game);
        }

        public void SetStatus(HudStatusEvent ev, string displayText = "")
        {
            if (visor != null && visor.Ready) { visor.Status(ev); return; }
            if (generated != null && generated.Ready) { generated.Status(ev); return; }
            if (statusText == null) return;
            if (statusRoutine != null) StopCoroutine(statusRoutine);
            statusText.color = StatusColor(ev);
            statusRoutine = StartCoroutine(ShowStatusBriefly(displayText));
        }

        private static string HandReadout(string label, GravityHand hand)
        {
            if (hand == null) return $"{label}  STANDBY";
            if (hand.Held != null) return $"{label}  HOLDING · {ShortName(hand.Held.displayName)}";
            if (hand.Candidate != null) return $"{label}  LOCKED · {ShortName(hand.Candidate.displayName)}";
            return $"{label}  STANDBY · AIM AT TARGET";
        }

        private static string ShortName(string value)
        {
            if (string.IsNullOrEmpty(value)) return "TARGET";
            return value.Length <= 8 ? value : value.Substring(0, 8) + "…";
        }

        private void ApplyReadoutFeedback(GravityGameManager game)
        {
            var pulse = .78f + Mathf.Sin(Time.unscaledTime * 7f) * .22f;
            var shieldRatio = Mathf.Clamp01(game.Shield / 100f);
            if (shieldText != null) shieldText.color = shieldRatio <= .3f
                ? new Color(1f, .18f, .24f, pulse)
                : shieldBaseColor;
            if (shieldFill != null) shieldFill.color = shieldRatio <= .3f
                ? new Color(1f, .14f, .22f, pulse)
                : new Color(.2f, .86f, 1f, .95f);
            var ultimateReady = game.UltimateCharge >= game.UltimateRequirement;
            if (energyText != null) energyText.color = ultimateReady
                ? new Color(1f, .82f, .24f, pulse)
                : energyBaseColor;
            if (energyFill != null) energyFill.color = ultimateReady
                ? new Color(1f, .66f, .18f, pulse)
                : new Color(.56f, .34f, 1f, .95f);
            if (repulseText != null) repulseText.color = game.RepulseCooldown <= 0f
                ? new Color(.3f, 1f, .7f, 1f)
                : repulseBaseColor;
        }

        private static Color StatusColor(HudStatusEvent ev) => ev switch
        {
            HudStatusEvent.Danger  => new Color(1f, .34f, .4f, 1f),
            HudStatusEvent.Locked  => new Color(.72f, .62f, 1f, 1f),
            HudStatusEvent.Holding => new Color(.72f, .62f, 1f, 1f),
            HudStatusEvent.Impact  => new Color(.38f, 1f, .76f, 1f),
            _                      => new Color(.82f, .92f, 1f, 1f),
        };

        public void ShowRunSummary(GravityRunSummary summary)
        {
            if (defeatPanel == null) return;
            EnsureResultCards();
            EnsureCameraHud();
            defeatPanel.transform.SetAsLastSibling();
            defeatPanel.SetActive(true);
            EnsureResultCards();
            resultLeftText.text =
                $"<size=28><color=#FFD16A>RUN COMPLETE</color></size>\n\n" +
                $"<size=32><color=#FFFFFF>TOTAL SCORE  {summary.TotalScore:00000}</color></size>\n\n" +
                $"WAVES CLEARED  {summary.completedWaves:00}\n" +
                $"TARGETS DOWN  {summary.enemiesDefeated:00}\n\n" +
                $"COMBAT SCORE  +{summary.baseCombatScore}\n" +
                $"WAVE BONUS  +{summary.waveScore}";
            resultRightText.text =
                $"<size=24><color=#78E8FF>BONUS RECORD</color></size>\n\n" +
                $"PRECISION ×{summary.precisionDefenseCount}   +{summary.precisionDefenseBonus}\n" +
                $"REFLECTION ×{summary.reflectAttackCount}   +{summary.reflectAttackBonus}\n" +
                $"REPULSION ×{summary.repulseAttackCount}   +{summary.repulseAttackBonus}\n" +
                $"COLLISION ×{summary.collisionAttackCount}   +{summary.collisionAttackBonus}\n" +
                $"CHAIN REACTION ×{summary.environmentAttackCount}   +{summary.environmentAttackBonus}\n" +
                $"SINGULARITY ×{summary.ultimateAttackCount}   +{summary.ultimateAttackBonus}";
            defeatText.text = "[Press Right A]: REDEPLOY";
            if (generated != null && generated.Ready) generated.Results(summary, resultLeftCard, resultRightCard, resultLeftText, resultRightText);
            visor?.SetVisible(false);
            visorDamage?.HoldDeathState();
            visor?.ApplyTopmostMaterials();
            Canvas.ForceUpdateCanvases();
        }

        public void HideRunSummary()
        {
            if (defeatPanel != null) defeatPanel.SetActive(false);
            if (generated != null) generated.SetCombatVisible(true);
            if (visor != null && visor.Ready) { generated?.SetCombatVisible(false); visor.SetVisible(true); }
            visorDamage?.ResetForCombat();
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
            EnsureHelmetFrames(layout);
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
            generated = GetComponent<GravityGeneratedHud>() ?? gameObject.AddComponent<GravityGeneratedHud>();
            generated.Configure(this);
            visor = GetComponent<GravityVisorHud>() ?? gameObject.AddComponent<GravityVisorHud>();
            visor.Configure(this);
            visorDamage = GetComponent<GravityVisorDamage>() ?? gameObject.AddComponent<GravityVisorDamage>();
            visorDamage.Configure(this);
            Debug.Log($"GRAVITY_EDGE_HUD_CONFIG canvas={rootCanvas?.renderMode} profile={ActiveSizeProfile} reference={ActiveReferenceResolution.x:0}x{ActiveReferenceResolution.y:0} distance={ActivePlaneDistance:0.00} camera={rootCanvas?.worldCamera?.name ?? "missing"} backplate=off");
        }

        private void EnsureHelmetFrames(HudLayout layout)
        {
            topLeftFrame = CreateHelmetFrame("HUD_FRAME_TOP_LEFT", true, new Color(.16f, .86f, 1f),
                layout.EdgeMargin - layout.ScaleX(14f), layout.ScaleY(380f), layout.ScaleSize(new Vector2(390f, 126f)));
            topRightFrame = CreateHelmetFrame("HUD_FRAME_TOP_RIGHT", false, new Color(.62f, .34f, 1f),
                layout.EdgeMargin - layout.ScaleX(14f), layout.ScaleY(380f), layout.ScaleSize(new Vector2(390f, 126f)));
            bottomLeftFrame = CreateHelmetFrame("HUD_FRAME_BOTTOM_LEFT", true, new Color(.2f, .86f, 1f),
                layout.EdgeMargin - layout.ScaleX(14f), layout.ScaleY(-345f), layout.ScaleSize(new Vector2(390f, 202f)));
            bottomRightFrame = CreateHelmetFrame("HUD_FRAME_BOTTOM_RIGHT", false, new Color(.72f, .35f, 1f),
                layout.EdgeMargin - layout.ScaleX(14f), layout.ScaleY(-326f), layout.ScaleSize(new Vector2(390f, 158f)));

            // Frames are decorative and always stay behind the data readouts.
            bottomRightFrame?.SetAsFirstSibling();
            bottomLeftFrame?.SetAsFirstSibling();
            topRightFrame?.SetAsFirstSibling();
            topLeftFrame?.SetAsFirstSibling();
        }

        private RectTransform CreateHelmetFrame(string name, bool left, Color accent, float margin, float y, Vector2 size)
        {
            var child = transform.Find(name);
            var frame = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            frame.transform.SetParent(transform, false);
            frame.SetActive(true);
            var rect = frame.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(left ? 0f : 1f, .5f);
            rect.pivot = new Vector2(left ? 0f : 1f, .5f);
            rect.anchoredPosition = new Vector2(left ? margin : -margin, y);
            rect.sizeDelta = size;
            var image = frame.GetComponent<Image>();
            image.color = new Color(.008f, .055f, .09f, .42f);
            image.raycastTarget = false;

            CreateHelmetLine(frame.transform, "ACCENT_TOP", accent,
                new Vector2(0f, size.y * .5f - 1.5f), new Vector2(size.x, 3f), new Vector2(.5f, .5f));
            CreateHelmetLine(frame.transform, "ACCENT_SIDE", accent,
                new Vector2((left ? -1f : 1f) * (size.x * .5f - 1.5f), 0f), new Vector2(3f, size.y), new Vector2(.5f, .5f));
            return rect;
        }

        private static void CreateHelmetLine(Transform parent, string name, Color color, Vector2 position, Vector2 size, Vector2 pivot)
        {
            var child = parent.Find(name);
            var line = child != null ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            line.transform.SetParent(parent, false);
            var rect = line.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = line.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
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
            // EN: no failure text expected in English result panel
            if (!ResultTextContent.Contains("TOTAL SCORE") || !ResultTextContent.Contains("WAVES CLEARED") || !ResultTextContent.Contains("PRECISION") || !ResultTextContent.Contains("REFLECTION")) failure += "result_breakdown_incomplete;";
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
                rootCanvas.sortingOrder = GravityVisorHud.SortOrder;
                rootCanvas.worldCamera = camera;
                var picoScaler = GetComponent<CanvasScaler>();
                if (picoScaler != null)
                {
                    picoScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    picoScaler.scaleFactor = 1f;
                    picoScaler.referencePixelsPerUnit = 100f;
                    picoScaler.referenceResolution = PicoReferenceResolution;
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
            // Unity owns a screen-space canvas transform after it is configured.
            // Rewriting it in LateUpdate invalidates the matrix used by the aim cursor
            // until the canvas rebuild, causing a one-frame disappearance after motion.
            if (CameraHudConfigured && rootCanvas.worldCamera == camera) return;
            rootCanvas.renderMode = RenderMode.ScreenSpaceCamera;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = GravityVisorHud.SortOrder;
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
            resultLeftCard = CreateResultCard("RESULT_LEFT_CARD_RUN_COMPLETE", layout.Scale(new Vector2(-285f, 0f)), layout.ScaleSize(new Vector2(440f, 470f)), new Color(.025f, .045f, .09f, .92f), out resultLeftText);
            resultRightCard = CreateResultCard("RESULT_RIGHT_CARD_BONUS_RECORD", layout.Scale(new Vector2(285f, 0f)), layout.ScaleSize(new Vector2(440f, 470f)), new Color(.035f, .035f, .085f, .92f), out resultRightText);
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
            public float ScaleX(float value) => value * horizontalScale;
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
