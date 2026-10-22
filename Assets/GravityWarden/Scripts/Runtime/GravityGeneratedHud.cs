using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    /// <summary>Generated panel typography with separately positioned live values.</summary>
    public sealed class GravityGeneratedHud : MonoBehaviour
    {
        private readonly Dictionary<string, Texture2D> textures = new();
        private GravityHud hud;
        private RawImage leftState, rightState, notice;
        private float noticeUntil;
        private Material keyMaterial;
        private Font numberFont;
        private static bool Pico => Application.platform == RuntimePlatform.Android;
        public bool Ready => Texture("HudTopLeft") != null && Texture("ResultLeft") != null;

        private Texture2D Texture(string name)
        {
            if (!textures.TryGetValue(name, out var texture)) { texture = Resources.Load<Texture2D>("GravityUI/" + name); textures[name] = texture; }
            return texture;
        }

        public void Configure(GravityHud owner)
        {
            hud = owner; if (!Ready) return;
            keyMaterial = Resources.Load<Material>("GravityUI/HudKey");
            numberFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            float width = Pico ? 320f : 340f;
            float height = Pico ? 310f : 330f;
            float x = Pico ? 740f : 585f, y = Pico ? 225f : 300f;
            var tl = Panel("GENERATED_TOP_LEFT", "HudTopLeft", transform, new Vector2(-x,y), new Vector2(width,height));
            var tr = Panel("GENERATED_TOP_RIGHT", "HudTopRight", transform, new Vector2(x,y), new Vector2(width,height));
            var bl = Panel("GENERATED_BOTTOM_LEFT", "HudBottomLeft", transform, new Vector2(-x,-y), new Vector2(width,height));
            var br = Panel("GENERATED_BOTTOM_RIGHT", "HudBottomRight", transform, new Vector2(x,-y), new Vector2(width,height));
            // Each fraction references the actual generated image's row centres.
            Number(hud.waveText, tl, .76f, .40f, Pico ? 38 : 29);
            Number(hud.enemyText, tl, .76f, .60f, Pico ? 38 : 29);
            Number(hud.scoreText, tr, .77f, .40f, Pico ? 34 : 27);
            Number(hud.energyText, tr, .77f, .60f, Pico ? 27 : 23);
            Number(hud.repulseText, bl, .77f, .42f, Pico ? 34 : 27);
            Number(hud.shieldText, br, .77f, .42f, Pico ? 34 : 27);
            leftState = Panel("GENERATED_LEFT_STATE", "Idle", bl.transform, new Vector2(0,-height*.12f), new Vector2(width*.38f,height*.17f));
            rightState = Panel("GENERATED_RIGHT_STATE", "Idle", br.transform, new Vector2(0,-height*.12f), new Vector2(width*.38f,height*.17f));
            notice = Panel("GENERATED_STATUS", "Ready", transform, new Vector2(-x,0), new Vector2(width*.46f,height*.18f));
            notice.gameObject.SetActive(false);
            foreach (var name in new[] { "HUD_FRAME_TOP_LEFT", "HUD_FRAME_TOP_RIGHT", "HUD_FRAME_BOTTOM_LEFT", "HUD_FRAME_BOTTOM_RIGHT" })
            { var old = transform.Find(name); if (old != null) old.gameObject.SetActive(false); }
            hud.leftText.gameObject.SetActive(false); hud.rightText.gameObject.SetActive(false); hud.statusText.gameObject.SetActive(false);
            foreach(var fill in new[] { hud.shieldFill, hud.energyFill, hud.repulseFill }) if (fill != null) fill.transform.parent.gameObject.SetActive(false);
            Debug.Log("GRAVITY_GENERATED_HUD_READY fixedText=Tripo_NanoBanana liveValues=Unity corners=4");
        }

        public void Refresh(GravityGameManager game)
        {
            hud.waveText.text = game.Wave.ToString("00");
            hud.enemyText.text = game.AliveEnemies.Count.ToString("00");
            hud.scoreText.text = game.Score.ToString("00000");
            hud.energyText.text = $"{Mathf.FloorToInt(game.UltimateCharge)}/{game.UltimateRequirement}";
            hud.shieldText.text = Mathf.CeilToInt(game.Shield).ToString("000");
            hud.repulseText.text = game.RepulseCooldown.ToString("0.0");
            if (leftState != null) leftState.texture = Texture(State(game.leftHand));
            if (rightState != null) rightState.texture = Texture(State(game.rightHand));
            if (notice != null && Time.unscaledTime >= noticeUntil) notice.gameObject.SetActive(false);
        }
        private static string State(GravityHand hand) => hand != null && hand.Held != null ? "Holding" : hand != null && hand.Candidate != null ? "Locked" : "Idle";

        public void Status(HudStatusEvent ev)
        {
            if (notice == null) return;
            string texName = ev switch {
                HudStatusEvent.Danger  => "Danger",
                HudStatusEvent.Holding => "Holding",
                HudStatusEvent.Locked  => "Locked",
                HudStatusEvent.Impact  => "Impact",
                _                      => "Ready",
            };
            notice.texture = Texture(texName); notice.gameObject.SetActive(true); noticeUntil = Time.unscaledTime + 1.65f;
        }

        public void Results(GravityRunSummary s, RectTransform left, RectTransform right, Text legacyLeft, Text legacyRight)
        {
            if (!Ready) return;
            SetCombatVisible(false);
            legacyLeft.enabled = legacyRight.enabled = false;
            hud.defeatText.enabled = false;
            left.GetComponent<Image>().color = right.GetComponent<Image>().color = Color.clear;
            float w = Pico ? 590f : 540f, h = Pico ? 530f : 486f;
            left.anchoredPosition = new Vector2(Pico ? -330f : -295f,0);
            right.anchoredPosition = new Vector2(Pico ? 330f : 295f,0);
            left.sizeDelta = right.sizeDelta = new Vector2(w,h);
            var lp = Panel("RESULT_ART", "ResultLeft", left, Vector2.zero, new Vector2(w,h));
            var rp = Panel("RESULT_ART", "ResultRight", right, Vector2.zero, new Vector2(w,h));
            var values = new[] { s.TotalScore.ToString("00000"), s.completedWaves.ToString("00"), s.enemiesDefeated.ToString("00"), s.baseCombatScore.ToString(), s.waveScore.ToString() };
            for (int i=0; i<values.Length; i++) ResultNumber(lp, i, values[i]);
            var bonus = new[] { $"{s.precisionDefenseCount} / +{s.precisionDefenseBonus}", $"{s.reflectAttackCount} / +{s.reflectAttackBonus}", $"{s.repulseAttackCount} / +{s.repulseAttackBonus}", $"{s.collisionAttackCount} / +{s.collisionAttackBonus}", $"{s.environmentAttackCount} / +{s.environmentAttackBonus}", $"{s.ultimateAttackCount} / +{s.ultimateAttackBonus}" };
            for (int i=0; i<bonus.Length; i++) ResultNumber(rp, i, bonus[i]);
        }

        public void SetCombatVisible(bool visible)
        {
            foreach (var name in new[] { "GENERATED_TOP_LEFT", "GENERATED_TOP_RIGHT", "GENERATED_BOTTOM_LEFT", "GENERATED_BOTTOM_RIGHT" })
            { var panel = transform.Find(name); if (panel != null) panel.gameObject.SetActive(visible); }
            if (!visible && notice != null) notice.gameObject.SetActive(false);
        }

        private void ResultNumber(RawImage parent, int row, string value)
        {
            string name = "LIVE_VALUE_" + row;
            var t = parent.transform.Find(name);
            if (t == null) { t = new GameObject(name, typeof(RectTransform), typeof(Text)).transform; t.SetParent(parent.transform,false); }
            var label = t.GetComponent<Text>(); Number(label,parent,.78f,(142f + row*52f)/494f, Pico ? 29 : 24); label.text = value;
            label.material = Resources.Load<Material>("GravityUI/Topmost");
        }

        private void Number(Text text, RawImage panel, float x, float y, int fontSize)
        {
            var r = text.rectTransform; r.SetParent(panel.transform,false);
            r.anchorMin = r.anchorMax = new Vector2(x,1f-y); r.pivot = new Vector2(.5f,.5f);
            r.anchoredPosition = Vector2.zero; r.sizeDelta = new Vector2(panel.rectTransform.sizeDelta.x*.35f, Mathf.Max(44f,fontSize*1.3f));
            text.font = numberFont; text.fontSize = fontSize; text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter; text.color = new Color(.88f,.98f,1f);
            text.supportRichText = false; text.raycastTarget = false; text.enabled = true; text.gameObject.SetActive(true);
        }

        private RawImage Panel(string name, string resource, Transform parent, Vector2 pos, Vector2 size)
        {
            var t = parent.Find(name);
            if (t == null) { t = new GameObject(name,typeof(RectTransform),typeof(RawImage)).transform; t.SetParent(parent,false); }
            var image = t.GetComponent<RawImage>(); image.texture = Texture(resource); image.material = resource.StartsWith("Result") ? Resources.Load<Material>("GravityUI/Topmost") : keyMaterial;
            image.color = Color.white; image.raycastTarget = false;
            var r = image.rectTransform; r.anchorMin = r.anchorMax = r.pivot = new Vector2(.5f,.5f); r.anchoredPosition = pos; r.sizeDelta = size;
            t.gameObject.SetActive(true); return image;
        }
    }
}
