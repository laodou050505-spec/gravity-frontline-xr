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
        public bool WorldSpaceConfigured
        {
            get
            {
                rootCanvas ??= GetComponent<Canvas>();
                return rootCanvas != null
                       && rootCanvas.renderMode == RenderMode.WorldSpace
                       && transform.parent == null
                       && transform.localScale.x > .0004f
                       && transform.localScale.x < .01f;
            }
        }
        public bool ResultLayoutConfigured => resultLeftCard != null && resultRightCard != null && resultLeftText != null && resultRightText != null;
        public bool ResultPanelVisible => defeatPanel != null && defeatPanel.activeSelf;
        public bool TopmostLayerConfigured => WorldSpaceConfigured;
        public string ResultTextContent => (resultLeftText != null ? resultLeftText.text : string.Empty) + "\n" + (resultRightText != null ? resultRightText.text : string.Empty);

        private void Awake()
        {
            uiFont = Font.CreateDynamicFontFromOSFont(new[] { "PingFang SC", "Noto Sans CJK SC", "Arial Unicode MS", "Arial" }, 28);
            EnsureResultCards();
            foreach (var label in GetComponentsInChildren<Text>(true)) label.font = uiFont;
            ConfigureWorldMissionBoard();
            HideRunSummary();
        }

        private void LateUpdate() => EnsureWorldMissionBoard();

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
            EnsureWorldMissionBoard();
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

        private void ConfigureWorldMissionBoard()
        {
            EnsureWorldMissionBoard();
            var backplate = transform.Find("HUD_Backplate_XRReadable");
            if (backplate != null)
            {
                backplate.gameObject.SetActive(true);
                var plate = backplate.GetComponent<Image>();
                if (plate != null) plate.color = new Color(.015f, .025f, .06f, .82f);
            }

            Place(waveText, new Vector2(-400f, 285f), new Vector2(350f, 48f), TextAnchor.MiddleLeft, 25);
            Place(enemyText, new Vector2(-400f, 230f), new Vector2(350f, 42f), TextAnchor.MiddleLeft, 22);
            Place(scoreText, new Vector2(400f, 285f), new Vector2(350f, 48f), TextAnchor.MiddleRight, 25);
            Place(energyText, new Vector2(400f, 230f), new Vector2(350f, 42f), TextAnchor.MiddleRight, 22);
            Place(leftText, new Vector2(-400f, 105f), new Vector2(350f, 38f), TextAnchor.MiddleLeft, 20);
            Place(rightText, new Vector2(400f, 105f), new Vector2(350f, 38f), TextAnchor.MiddleRight, 20);
            Place(repulseText, new Vector2(-400f, 38f), new Vector2(350f, 34f), TextAnchor.MiddleLeft, 19);
            Place(shieldText, new Vector2(400f, 38f), new Vector2(350f, 34f), TextAnchor.MiddleRight, 19);
            Place(statusText, new Vector2(0f, -96f), new Vector2(900f, 48f), TextAnchor.MiddleCenter, 23);
            PlaceTrack(repulseFill, new Vector2(-400f, 0f), new Vector2(325f, 12f));
            PlaceTrack(shieldFill, new Vector2(400f, 0f), new Vector2(325f, 12f));
            PlaceTrack(energyFill, new Vector2(400f, 194f), new Vector2(325f, 12f));

            var help = transform.Find("Help")?.GetComponent<Text>();
            Place(help, new Vector2(0f, -255f), new Vector2(1100f, 34f), TextAnchor.MiddleCenter, 17);
            if (help != null)
            {
                help.color = new Color(.62f, .72f, .8f, .72f);
                help.text = Application.isMobilePlatform
                    ? "PICO  扳机/握把吸取 · 合拢对撞 · 左 X 斥力 · 右 A 奇点 · 左 Y 归位"
                    : "桌面  Q/E 吸取 · Space 对撞 · F 斥力 · R 奇点 · C 归位";
                StartCoroutine(HideHelpAfterDelay(help));
            }

            foreach (var label in GetComponentsInChildren<Text>(true)) label.raycastTarget = false;
            foreach (var image in GetComponentsInChildren<Image>(true)) image.raycastTarget = false;
            var raycaster = GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;

            EdgeLayoutConfigured = WorldSpaceConfigured && backplate != null && backplate.gameObject.activeSelf;
        }

        public bool ValidateViewportLayout(Camera camera, out string failure)
        {
            failure = string.Empty;
            if (camera == null)
            {
                failure = "camera missing";
                return false;
            }
            if (!WorldSpaceConfigured) failure += "canvas_not_world_space;";
            var designStart = FindFirstObjectByType<GravityDesignStart>();
            if (designStart == null) failure += "design_start_missing;";
            else
            {
                var distance = Vector3.Distance(transform.position, designStart.MissionBoardPosition);
                if (distance > .03f) failure += $"mission_board_not_at_design_offset[{distance:0.000}];";
                var facing = Vector3.Dot(transform.forward, (designStart.WorldEyePosition - transform.position).normalized);
                if (facing < .96f) failure += $"mission_board_not_facing_start[{facing:0.000}];";
            }
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
            if (!WorldSpaceConfigured) failure += "result_canvas_not_world_space;";
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
            if (!WorldSpaceConfigured) failure += "mission_board_not_world_space;";
            if (Vector3.Distance(transform.position, camera.transform.position) < 2.5f) failure += "mission_board_too_close_to_head;";
            return failure.Length == 0;
        }

        private IEnumerator ShowStatusBriefly(string message)
        {
            statusText.text = message;
            statusText.canvasRenderer.SetAlpha(1f);
            yield return new WaitForSeconds(1.65f);
            statusText.CrossFadeAlpha(0f, .42f, true);
            yield return new WaitForSeconds(.45f);
            statusText.text = string.Empty;
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

        private void EnsureWorldMissionBoard()
        {
            rootCanvas ??= GetComponent<Canvas>();
            if (rootCanvas == null) return;
            if (transform.parent != null) transform.SetParent(null, true);
            rootCanvas.renderMode = RenderMode.WorldSpace;
            rootCanvas.overrideSorting = true;
            rootCanvas.sortingOrder = 20;
            rootCanvas.planeDistance = 1f;
            rootCanvas.worldCamera = Camera.main;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null)
            {
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                scaler.dynamicPixelsPerUnit = 1f;
            }
            if (transform is RectTransform rect)
            {
                rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                rect.pivot = new Vector2(.5f, .5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(1200f, 760f);
            }
            var designStart = FindFirstObjectByType<GravityDesignStart>();
            if (designStart != null)
            {
                transform.SetPositionAndRotation(designStart.MissionBoardPosition, designStart.MissionBoardRotation);
                transform.localScale = Vector3.one * .0034f;
                // A root RectTransform serializes its anchored XY independently of its Z.
                // Keep both representations derived from the one design start, so reopening
                // the scene cannot silently collapse this physical terminal onto the origin.
                if (transform is RectTransform boardRect)
                    boardRect.anchoredPosition3D = designStart.MissionBoardPosition;
            }
        }

        private void EnsureResultCards()
        {
            if (defeatPanel == null) return;
            var panelRect = defeatPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1120f, 630f);
            var panelImage = defeatPanel.GetComponent<Image>();
            if (panelImage != null)
            {
                panelImage.color = Color.clear;
                panelImage.raycastTarget = false;
            }
            resultLeftCard = CreateResultCard("RESULT_LEFT_CARD_本轮结算", new Vector2(-285f, 0f), new Vector2(440f, 470f), new Color(.025f, .045f, .09f, .92f), out resultLeftText);
            resultRightCard = CreateResultCard("RESULT_RIGHT_CARD_额外奖励", new Vector2(285f, 0f), new Vector2(440f, 470f), new Color(.035f, .035f, .085f, .92f), out resultRightText);
            Place(defeatText, new Vector2(0f, -288f), new Vector2(760f, 42f), TextAnchor.MiddleCenter, 21);
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
