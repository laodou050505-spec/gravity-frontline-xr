using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    /// <summary>Opt-in evidence using the real damage, healing, and summary paths.</summary>
    public sealed class GravityVisorDamageEvidence : MonoBehaviour
    {
        private readonly List<string> results = new();
        private readonly List<UnityEngine.Object> probes = new();
        private string output;
        private int passed, failed;
        private GravityGameManager game;
        private GravityVisorDamage damage;
        private Camera view;
        private RawImage originalGlass, faultGlass, centralGlass;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravityDamageEvidence")) return;
            if (FindFirstObjectByType<GravityVisorDamageEvidence>() != null) return;
            var instance = new GameObject("EVIDENCE_ONLY_VisorDeath0910");
            DontDestroyOnLoad(instance);
            instance.AddComponent<GravityVisorDamageEvidence>();
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-gravityDamageOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.GetFullPath(Path.Combine("Evidence", "VisorDeath0910"));
            Directory.CreateDirectory(output);
            Application.runInBackground = true;
            Screen.SetResolution(1600, 1000, false);
            var routine = Run();
            while (true)
            {
                object next;
                try
                {
                    if (!routine.MoveNext()) break;
                    next = routine.Current;
                }
                catch (Exception exception)
                {
                    Check(false, "unexpected_exception", exception.ToString());
                    break;
                }
                yield return next;
            }
            foreach (var probe in probes) if (probe != null) Destroy(probe);
            string summary = $"GRAVITY_DAMAGE_EVIDENCE_{(failed == 0 ? "PASS" : "FAIL")} passed={passed} failed={failed} output={output}";
            results.Add(summary);
            File.WriteAllLines(Path.Combine(output, "Results.txt"), results);
            if (failed == 0) Debug.Log(summary); else Debug.LogError(summary);
            Application.Quit(failed == 0 ? 0 : 2);
        }

        private IEnumerator Run()
        {
            yield return new WaitForSecondsRealtime(2.5f);
            var title = FindFirstObjectByType<GravityTitleController>();
            var titleHud = FindFirstObjectByType<GravityTitleHud>();
            float deadline = Time.unscaledTime + 12f;
            while (title != null && (!title.IsReadyForButtonInput || titleHud != null && !titleHud.IsSpatialReady)
                   && Time.unscaledTime < deadline) yield return null;
            if (!Check(title != null && title.IsReadyForButtonInput, "title_ready", $"title={title != null}")) yield break;
            Capture("00_Title.png");
            title.StartGame();
            deadline = Time.unscaledTime + 20f;
            while (game == null && Time.unscaledTime < deadline)
            {
                game = FindFirstObjectByType<GravityGameManager>();
                yield return null;
            }
            if (!Check(game != null && game.hud != null, "game_started", $"game={game != null}")) yield break;
            view = Camera.main;
            damage = game.hud.GetComponent<GravityVisorDamage>();
            if (!Check(view != null && damage != null && damage.Ready, "damage_component_ready",
                    $"camera={view != null} component={damage != null} ready={damage != null && damage.Ready}")) yield break;

            // Retain normal manager updates so its production grace period and damage
            // cooldown elapse. Disable only autonomous attackers in this opt-in run.
            foreach (var desktop in FindObjectsByType<DesktopFallbackController>(FindObjectsSortMode.None)) desktop.enabled = false;
            if (game.leftHand != null) game.leftHand.enabled = false;
            if (game.rightHand != null) game.rightHand.enabled = false;
            float readyAt = Time.unscaledTime + 2f;
            while (Time.unscaledTime < readyAt)
            {
                SuppressAutonomousDamage();
                yield return null;
            }
            Check(Mathf.Abs(game.Shield - 100f) < .01f && damage.Visible && damage.DamageRatio < .001f
                  && damage.VisibleDamage < .005f && damage.HitPulse < .005f,
                "full_health_clear", State());
            Check(damage.CrackTexture != null && damage.CrackTexture.width >= 256 && damage.CrackTexture.height >= 256
                  && Resources.Load<Texture2D>("GravityUI/VisorGlassCracks") != null,
                "generated_crack_texture_loaded", damage.CrackTexture != null
                    ? $"texture={damage.CrackTexture.name} size={damage.CrackTexture.width}x{damage.CrackTexture.height}"
                    : "texture=null");
            BindDamageLayers();
            Check(damage.CenterTexture != null && damage.CenterTexture.width >= 256
                  && damage.CenterTexture != damage.CrackTexture
                  && Resources.Load<Texture2D>("GravityUI/VisorCentralCracks") == damage.CenterTexture,
                "independent_generated_central_texture", damage.CenterTexture != null
                    ? $"texture={damage.CenterTexture.name} size={damage.CenterTexture.width}x{damage.CenterTexture.height}"
                    : "texture=null");
            Check(damage.FaultMaterial != null && damage.CenterMaterial != null
                  && damage.FaultMaterial != damage.CenterMaterial
                  && damage.FaultMaterial.shader.name == "GravityWarden/UI/VisorSupplement"
                  && damage.CenterMaterial.shader.name == "GravityWarden/UI/VisorSupplement"
                  && damage.FaultMaterial.GetFloat("_Layer") < .5f && damage.CenterMaterial.GetFloat("_Layer") > .5f,
                "separate_fault_and_central_materials", $"fault={damage.FaultMaterial != null} center={damage.CenterMaterial != null}");
            var material = damage.OverlayMaterial;
            Check(material != null && material.shader != null && material.shader.name == "GravityWarden/UI/VisorDamage"
                  && material.renderQueue >= 4000 && Resources.Load<Material>("GravityUI/VisorDamage") != null,
                "topmost_overlay_material", material != null
                    ? $"shader={material.shader.name} renderQueue={material.renderQueue}"
                    : "material=null");
            Check(!material.HasProperty("_Darkening") && !material.HasProperty("_CentralCracks")
                  && originalGlass.texture == damage.CrackTexture && originalGlass.material == material,
                "original_glass_uses_separate_texture_and_layer", $"texture={originalGlass.texture.name} shader={material.shader.name}");
            CheckOriginalGlassLayout("normal_projection");
            CheckCentralTextureAspect("normal_projection");
            Check(damage.CoversViewport(view,out var coverage),"fault_covers_viewport",coverage);
            Check(faultGlass.transform.GetSiblingIndex()==0 && originalGlass.transform.GetSiblingIndex()==1
                  && centralGlass.transform.GetSiblingIndex()==2 && !faultGlass.raycastTarget
                  && !originalGlass.raycastTarget && !centralGlass.raycastTarget,
                "damage_layer_composite_order", $"fault={faultGlass.transform.GetSiblingIndex()} original={originalGlass.transform.GetSiblingIndex()} central={centralGlass.transform.GetSiblingIndex()}");
            var damageRoot=originalGlass.transform.parent;
            var readoutRoot=game.hud.transform.Find("VISOR_TOPMOST_CANVAS");
            Check(readoutRoot!=null && readoutRoot.parent==damageRoot.parent
                  && readoutRoot.GetSiblingIndex()>damageRoot.GetSiblingIndex(),
                "readouts_above_all_damage_layers",
                $"damageRoot={damageRoot.GetSiblingIndex()} readoutRoot={(readoutRoot!=null?readoutRoot.GetSiblingIndex():-1)}");
            if (!view.stereoEnabled)
            {
                float originalFov=view.fieldOfView, originalAspect=view.aspect;
                view.fieldOfView=110f; view.aspect=1.65f;
                yield return null; yield return new WaitForEndOfFrame();
                Check(damage.CoversViewport(view,out var wideCoverage),"fault_covers_wide_projection",wideCoverage+" fov=110 aspect=1.65");
                CheckOriginalGlassLayout("wide_projection");
                CheckCentralTextureAspect("wide_projection");
                view.fieldOfView=originalFov; view.aspect=originalAspect;
                yield return null; yield return new WaitForEndOfFrame();
            }
            float brightness100=CheckShaderAppearance("health100",out _);
            Check(brightness100>.99f && damage.ScreenDarkening<.001f,"full_health_no_tint",$"brightness={F(brightness100)}");
            Capture("01_Health100.png");

            game.DamagePlayer(8);
            float firstPulse=damage.HitPulse;
            Check(Mathf.Abs(game.Shield-92f)<.01f && Mathf.Abs(damage.DamageRatio-.08f)<.001f
                  && firstPulse>.01f,"real_hit_immediate_feedback",State());
            yield return null;
            Capture("01a_HitPulse92.png");
            yield return new WaitForSecondsRealtime(2.5f);
            float edge92=CheckLightDamageGlass("health92",out var strength92);
            Capture("01b_Health92.png");

            game.DamagePlayer(8);
            yield return new WaitForSecondsRealtime(2.5f);
            float edge84=CheckLightDamageGlass("health84",out var strength84);
            Capture("01c_Health84.png");

            game.DamagePlayer(9);
            float pulse75=damage.HitPulse;
            yield return null;
            Capture("02a_HitPulse75.png");
            yield return new WaitForSecondsRealtime(2.5f);
            float damage75=damage.VisibleDamage;
            Check(Mathf.Abs(game.Shield-75f)<.01f && Mathf.Abs(damage75-.25f)<.035f && damage.HitPulse<pulse75*.1f,
                "health75_pulse_settled",State());
            float edge75=CheckLightDamageGlass("health75",out var strength75);
            Check(edge84>edge92 && edge75>edge84 && strength84>strength92 && strength75>strength84,
                "light_hit_glass_builds_gradually",
                $"renderedBrightness92={F(edge92)} 84={F(edge84)} 75={F(edge75)} strengths={F(strength92)}/{F(strength84)}/{F(strength75)}");
            float brightness75=CheckShaderAppearance("health75",out _);
            Check(brightness75<brightness100-.20f && brightness75>.60f && damage.ScreenDarkening>.25f,
                "health75_screen_darkens",$"brightness={F(brightness75)} "+State());
            Capture("02_Health75.png");
            CheckSceneDarkening("health75", .91f);

            game.DamagePlayer(30);
            yield return new WaitForSecondsRealtime(2.5f);
            float damage45 = damage.VisibleDamage;
            Check(Mathf.Abs(game.Shield - 45f) < .01f && Mathf.Abs(damage.DamageRatio - .55f) < .001f
                  && Mathf.Abs(damage45 - .55f) < .035f && damage45 > damage75 + .2f,
                "health45_stronger", State());
            float brightness45=CheckShaderAppearance("health45",out _);
            Check(brightness45<brightness75-.15f,"health45_screen_darker",$"brightness={F(brightness45)} "+State());
            Capture("03_Health45.png");
            CheckSceneDarkening("health45", .81f);

            game.DamagePlayer(30);
            yield return new WaitForSecondsRealtime(2.5f);
            float damage15 = damage.VisibleDamage;
            Check(Mathf.Abs(game.Shield - 15f) < .01f && Mathf.Abs(damage.DamageRatio - .85f) < .001f
                  && Mathf.Abs(damage15 - .85f) < .035f && damage15 > damage45 + .2f && damage.CriticalRatio > 0f,
                "health15_critical", State());
            float brightness15=CheckShaderAppearance("health15",out var centralCoverage);
            Check(brightness15<brightness45-.15f && brightness15>.16f,
                "health15_dark_but_readable",$"brightness={F(brightness15)} "+State());
            Check(centralCoverage>.001f && centralCoverage<.12f,"central_cracks_sparse",
                $"centralAdditionalPixelRatio={F(centralCoverage)} opacity={F(damage.CentralCrackOpacity)}");
            Capture("04_Health15.png");
            CheckSceneDarkening("health15", .68f);
            CheckLocalFaultAnimation();

            // A real opaque object close to the camera gives a visual check that the
            // crack material still renders over held scene geometry.
            var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
            blocker.name = "EVIDENCE_ONLY_DamageForegroundOccluder";
            Destroy(blocker.GetComponent<Collider>());
            blocker.transform.SetParent(view.transform, false);
            blocker.transform.localPosition = new Vector3(0f, 0f, .55f);
            blocker.transform.localScale = new Vector3(5f, 5f, .05f);
            var blockerMaterial = new Material(Shader.Find("Unlit/Color"));
            blockerMaterial.color = new Color(.22f, .13f, .08f);
            blocker.GetComponent<Renderer>().sharedMaterial = blockerMaterial;
            probes.Add(blocker);
            probes.Add(blockerMaterial);
            Capture("04b_Health15Foreground.png");
            blocker.SetActive(false);

            if (!Check(game.healthPackPrefab != null && game.healthPackPrefab.GetComponent<GravityTarget>() != null,
                    "health_pack_prefab", $"prefab={game.healthPackPrefab != null}")) yield break;
            for (int i = 0; i < 2; i++)
            {
                var pack = Instantiate(game.healthPackPrefab, view.transform.position + view.transform.forward * 4f, Quaternion.identity);
                probes.Add(pack);
                var target = pack.GetComponent<GravityTarget>();
                game.ConsumeHealthPack(target);
                Check(Mathf.Abs(game.Shield - (50f + 35f * i)) < .01f, "real_health_pack_" + (i + 1), State());
                yield return null;
            }
            yield return new WaitForSecondsRealtime(2.5f);
            Check(Mathf.Abs(game.Shield - 85f) < .01f && Mathf.Abs(damage.DamageRatio - .15f) < .001f
                  && Mathf.Abs(damage.VisibleDamage - .15f) < .035f && damage.VisibleDamage < damage75
                  && damage.HitPulse < .005f && damage.CriticalRatio < .001f,
                "healing_reduces_damage", State());
            float brightness85=CheckShaderAppearance("healed85",out _);
            Check(brightness85>brightness75 && brightness85>brightness15+.25f,
                "healing_restores_screen_brightness",$"brightness={F(brightness85)} "+State());
            Capture("05_Healed85.png");
            CheckSceneDarkening("healed85", .96f);

            game.DamagePlayer(100);
            yield return null;
            Check(game.Shield<=0f && game.hud.ResultPanelVisible && damage.DeathStateHeld && damage.Visible
                  && faultGlass.gameObject.activeInHierarchy && centralGlass.gameObject.activeInHierarchy
                  && damage.DamageRatio>.999f && damage.VisibleDamage>=.85f && damage.ScreenDarkening>.70f,
                "real_defeat_keeps_broken_visor",State()+$" resultVisible={game.hud.ResultPanelVisible}");
            CheckSummaryAboveDamage();
            var deathParameters=VisualParameters();
            Capture("06_RunSummary.png");
            yield return new WaitForSecondsRealtime(2.6f);
            var heldParameters=VisualParameters();
            float heldDelta=deathParameters.Zip(heldParameters,(a,b)=>Mathf.Abs(a-b)).Max();
            Check(damage.DeathStateHeld && damage.Visible && faultGlass.gameObject.activeInHierarchy
                  && centralGlass.gameObject.activeInHierarchy && heldDelta<.0001f,
                "death_visual_and_fault_time_hold",
                $"parameterMaxDelta={heldDelta.ToString("F6",CultureInfo.InvariantCulture)} faultTime={F(damage.FaultMaterial.GetFloat("_FaultTime"))} "+State());
            Capture("06b_RunSummary_Held.png");

            // Enter and the right A button both call this existing production entry.
            // Invoke that entry rather than resetting shield or effects in the test.
            var restart=typeof(GravityGameManager).GetMethod("RestartGame",
                System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public|System.Reflection.BindingFlags.NonPublic);
            if(!Check(restart!=null,"real_redeploy_entry_exists","GravityGameManager.RestartGame"))yield break;
            var oldGame=game;
            restart.Invoke(null,null);
            game=null;damage=null;
            deadline=Time.unscaledTime+15f;
            while(Time.unscaledTime<deadline)
            {
                SuppressAutonomousDamage();
                var candidate=FindFirstObjectByType<GravityGameManager>();
                if(candidate!=null && !ReferenceEquals(candidate,oldGame) && candidate.hud!=null)
                {
                    var candidateDamage=candidate.hud.GetComponent<GravityVisorDamage>();
                    if(candidateDamage!=null && candidateDamage.Ready)
                    { game=candidate;damage=candidateDamage;break; }
                }
                yield return null;
            }
            if(!Check(game!=null && damage!=null,"real_redeploy_loaded_fresh_combat",$"game={game!=null} damage={damage!=null}"))yield break;
            view=Camera.main;
            BindDamageLayers();
            foreach(var desktop in FindObjectsByType<DesktopFallbackController>(FindObjectsSortMode.None))desktop.enabled=false;
            if(game.leftHand!=null)game.leftHand.enabled=false;
            if(game.rightHand!=null)game.rightHand.enabled=false;
            readyAt=Time.unscaledTime+.5f;
            while(Time.unscaledTime<readyAt){SuppressAutonomousDamage();yield return null;}
            Check(Mathf.Abs(game.Shield-100f)<.01f && !game.hud.ResultPanelVisible && !damage.DeathStateHeld
                  && damage.Visible && faultGlass.gameObject.activeInHierarchy && centralGlass.gameObject.activeInHierarchy
                  && damage.DamageRatio<.001f && damage.VisibleDamage<.001f && damage.HitPulse<.001f
                  && damage.ScreenDarkening<.001f && damage.FaultRatio<.001f && damage.CentralCrackOpacity<.001f,
                "real_redeploy_clears_all_damage",State());
            float brightnessRestart=CheckShaderAppearance("redeployed100",out var redeployCracks);
            Check(brightnessRestart>.99f && redeployCracks<.001f,"redeployed_shader_is_clear",
                $"brightness={F(brightnessRestart)} addedCracks={F(redeployCracks)}");
            CheckCentralTextureAspect("redeployed_projection");
            Capture("07_Redeployed100.png");
        }

        private static void SuppressAutonomousDamage()
        {
            foreach (var enemy in FindObjectsByType<RobotEnemy>(FindObjectsSortMode.None)) enemy.enabled = false;
            foreach (var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) projectile.gameObject.SetActive(false);
        }

        private string State() => $"shield={F(game.Shield)} targetDamage={F(damage.DamageRatio)} visibleDamage={F(damage.VisibleDamage)} hitPulse={F(damage.HitPulse)} critical={F(damage.CriticalRatio)} dim={F(damage.ScreenDarkening)} fault={F(damage.FaultRatio)} centreCracks={F(damage.CentralCrackOpacity)} edgeStrength={F(damage.EdgeCrackStrength)} deathHeld={damage.DeathStateHeld} visible={damage.Visible}";
        private static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);

        private bool Check(bool condition, string scenario, string detail)
        {
            if (condition) passed++; else failed++;
            string message = $"GRAVITY_DAMAGE_CASE_{(condition ? "PASS" : "FAIL")} {scenario} {detail}";
            results.Add(message);
            if (condition) Debug.Log(message); else Debug.LogError(message);
            return condition;
        }

        private void BindDamageLayers()
        {
            var layer=game.hud.transform.Find("VISOR_DAMAGE_CANVAS");
            originalGlass=layer.Find("VISOR_GLASS_DAMAGE").GetComponent<RawImage>();
            faultGlass=layer.Find("VISOR_DISPLAY_FAULT").GetComponent<RawImage>();
            centralGlass=layer.Find("VISOR_CENTRAL_CRACKS").GetComponent<RawImage>();
        }

        private void CheckCentralTextureAspect(string label)
        {
            var corners=new Vector3[4];
            centralGlass.rectTransform.GetWorldCorners(corners);
            float width=Vector3.Distance(corners[0],corners[3]);
            float height=Vector3.Distance(corners[0],corners[1]);
            var uv=centralGlass.uvRect;
            float pixelWidth=width/(damage.CenterTexture.width*Mathf.Abs(uv.width));
            float pixelHeight=height/(damage.CenterTexture.height*Mathf.Abs(uv.height));
            float ratio=pixelWidth/pixelHeight;
            Check(Mathf.Abs(ratio-1f)<.01f,"central_texture_square_pixels_"+label,
                $"worldSize={F(width)}x{F(height)} texture={damage.CenterTexture.width}x{damage.CenterTexture.height} uv={uv} pixelAspect={F(ratio)} profile={game.hud.ActiveSizeProfile}");
        }

        private float CheckLightDamageGlass(string label,out float strength)
        {
            const int size=512;
            var material=damage.OverlayMaterial;
            strength=damage.EdgeCrackStrength;
            float originalStrength=material.GetFloat("_EdgeStrength");
            float originalPulse=material.GetFloat("_HitPulse");
            Color32[] current,unsoftened;
            try
            {
                material.SetFloat("_HitPulse",0f);
                current=RenderShaderLayers(size,false,true,false,true);
                material.SetFloat("_EdgeStrength",1f);
                unsoftened=RenderShaderLayers(size,false,true,false,true);
            }
            finally
            {
                material.SetFloat("_EdgeStrength",originalStrength);
                material.SetFloat("_HitPulse",originalPulse);
            }
            float actual=0f,reference=0f;
            for(int i=0;i<current.Length;i++)
            { actual+=Luminance(current[i]);reference+=Luminance(unsoftened[i]); }
            float attenuation=reference>0?actual/reference:1f;
            Check(reference>0f && attenuation<.85f && strength<.85f,
                "light_hit_glass_softened_"+label,
                $"renderedRelativeStrength={F(attenuation)} actualPixelSum={F(actual)} originalPixelSum={F(reference)} strength={F(strength)} "+State());
            WritePixels("EdgeOnly_"+label+".png",current,size,size);
            return actual;
        }

        private float[] VisualParameters() => new[]
        {
            damage.DamageRatio,damage.VisibleDamage,damage.HitPulse,damage.ScreenDarkening,damage.FaultRatio,
            damage.CentralCrackOpacity,damage.EdgeCrackStrength,
            damage.OverlayMaterial.GetFloat("_Damage"),damage.OverlayMaterial.GetFloat("_HitPulse"),damage.OverlayMaterial.GetFloat("_EdgeStrength"),
            damage.FaultMaterial.GetFloat("_Darkening"),damage.FaultMaterial.GetFloat("_FaultRatio"),
            damage.FaultMaterial.GetFloat("_HitPulse"),damage.FaultMaterial.GetFloat("_FaultTime"),
            damage.CenterMaterial.GetFloat("_CentralCracks")
        };

        private void CheckSummaryAboveDamage()
        {
            var panel=game.hud.defeatPanel.transform;
            var glassBranch=originalGlass.transform;
            while(glassBranch.parent!=null && glassBranch.parent!=panel.parent)glassBranch=glassBranch.parent;
            var texts=game.hud.defeatPanel.GetComponentsInChildren<Text>();
            Check(panel.parent==glassBranch.parent && panel.GetSiblingIndex()>glassBranch.GetSiblingIndex()
                  && texts.Length>0 && texts.All(t=>t.material!=null && t.material.renderQueue>=4990),
                "summary_text_above_broken_visor",
                $"panelSibling={panel.GetSiblingIndex()} glassBranch={glassBranch.name}:{glassBranch.GetSiblingIndex()} visibleTexts={texts.Length}");
        }

        private void CheckOriginalGlassLayout(string label)
        {
            var rect=originalGlass.rectTransform;
            Check(Vector2.Distance(rect.sizeDelta,game.hud.ActiveReferenceResolution)<.01f
                  && rect.anchoredPosition.sqrMagnitude<.001f
                  && Vector3.Distance(rect.localScale,Vector3.one)<.001f
                  && Quaternion.Angle(rect.localRotation,Quaternion.identity)<.001f
                  && originalGlass.uvRect==new Rect(0,0,1,1),
                "original_glass_layout_"+label,
                $"size={rect.sizeDelta} reference={game.hud.ActiveReferenceResolution} position={rect.anchoredPosition} uv={originalGlass.uvRect}");
        }

        // Render the actual three UI layers with their projected geometry and UVs.
        // Comparing with/without the central layer isolates new fractures from the
        // original accepted edge glass and the independently dimmed display beneath.
        private float CheckShaderAppearance(string label,out float centralCoverage)
        {
            const int size=512;
            var baseline=RenderShaderLayers(size,false,true);
            var combined=RenderShaderLayers(size,true,true);
            float core=0; int coreCount=0, bright=0, total=0, coreAdditional=0;
            for(int y=size/4;y<size*3/4;y++)
            for(int x=size/4;x<size*3/4;x++)
            {
                int index=y*size+x;
                float luminance=Luminance(combined[index]);
                float added=luminance-Luminance(baseline[index]);
                if(added>.025f)bright++;
                total++;
                float u=(x+.5f)/size,v=(y+.5f)/size;
                if(Mathf.Abs(u-.5f)<.018f && Mathf.Abs(v-.5f)<.018f)
                {
                    core+=luminance;coreCount++;
                    if(added>.005f)coreAdditional++;
                }
            }
            centralCoverage=(float)bright/total;
            float brightness=core/Mathf.Max(1,coreCount);
            float expectedCore=1-damage.ScreenDarkening;
            Check(Mathf.Abs(brightness-expectedCore)<.035f && coreAdditional==0,
                "shader_"+label+"_clear_aim_core",
                $"coreBrightness={F(brightness)} nominal={F(expectedCore)} centralAdditionalPixels={bright}/{total} coreCrackPixels={coreAdditional}");
            WritePixels("Shader_"+label+".png",combined,size,size);
            return brightness;
        }

        private Color32[] RenderShaderLayers(int size,bool includeCentral,bool includeOriginal,bool includeFault=true,bool blackBackground=false)
        {
            var target=RenderTexture.GetTemporary(size,size,0,RenderTextureFormat.ARGB32,RenderTextureReadWrite.Linear);
            var previous=RenderTexture.active;
            var pixels=new Texture2D(size,size,TextureFormat.RGBA32,false,true);
            bool previousSrgb=GL.sRGBWrite;
            try
            {
                RenderTexture.active=target;
                GL.sRGBWrite=false;
                GL.Clear(true,true,blackBackground?Color.black:Color.white);
                GL.PushMatrix();
                try
                {
                    GL.LoadOrtho();
                    if(includeFault)DrawShaderLayer(faultGlass);
                    if(includeOriginal)DrawShaderLayer(originalGlass);
                    if(includeCentral)DrawShaderLayer(centralGlass);
                }
                finally { GL.PopMatrix(); }
                pixels.ReadPixels(new Rect(0,0,size,size),0,0);pixels.Apply();
                return pixels.GetPixels32();
            }
            finally
            {
                GL.sRGBWrite=previousSrgb;
                RenderTexture.active=previous;
                Destroy(pixels);
                RenderTexture.ReleaseTemporary(target);
            }
        }

        private void DrawShaderLayer(RawImage image)
        {
            var corners=new Vector3[4];
            image.rectTransform.GetWorldCorners(corners);
            for(int i=0;i<corners.Length;i++)corners[i]=view.WorldToViewportPoint(corners[i]);
            var uv=image.uvRect;
            image.material.SetTexture("_MainTex",image.texture);
            image.material.SetPass(0);
            GL.Begin(GL.QUADS);
            GL.Color(image.color);
            GL.TexCoord2(uv.xMin,uv.yMin);GL.Vertex3(corners[0].x,corners[0].y,0);
            GL.TexCoord2(uv.xMax,uv.yMin);GL.Vertex3(corners[3].x,corners[3].y,0);
            GL.TexCoord2(uv.xMax,uv.yMax);GL.Vertex3(corners[2].x,corners[2].y,0);
            GL.TexCoord2(uv.xMin,uv.yMax);GL.Vertex3(corners[1].x,corners[1].y,0);
            GL.End();
        }

        private void CheckLocalFaultAnimation()
        {
            const int size=256;
            var material=damage.FaultMaterial;
            float originalTime=material.GetFloat("_FaultTime");
            try
            {
                material.SetFloat("_FaultTime",0f);
                var first=RenderShaderLayers(size,false,false);
                float maxChangedFraction=0f,maxMeanChange=0f,maxCoreChange=0f;
                foreach(float time in new[]{.325f,.65f,.975f,1.3f,1.95f,2.6f})
                {
                    material.SetFloat("_FaultTime",time);
                    var sample=RenderShaderLayers(size,false,false);
                    int changed=0;float mean=0f;
                    for(int i=0;i<sample.Length;i++)
                    {
                        float delta=Mathf.Abs(Luminance(sample[i])-Luminance(first[i]));
                        if(delta>.006f)changed++;
                        mean+=delta;
                        int x=i%size,y=i/size;
                        if(Mathf.Abs((x+.5f)/size-.5f)<.025f && Mathf.Abs((y+.5f)/size-.5f)<.025f)
                            maxCoreChange=Mathf.Max(maxCoreChange,delta);
                    }
                    maxChangedFraction=Mathf.Max(maxChangedFraction,(float)changed/sample.Length);
                    maxMeanChange=Mathf.Max(maxMeanChange,mean/sample.Length);
                }
                Check(damage.FaultRatio>.5f && maxChangedFraction>.001f && maxChangedFraction<.20f
                      && maxMeanChange<.025f && maxCoreChange<.005f,
                    "mechanical_fault_animates_local_rows_only",
                    $"changedFraction={F(maxChangedFraction)} meanChange={F(maxMeanChange)} aimCoreChange={F(maxCoreChange)} severity={F(damage.FaultRatio)}");
            }
            finally { material.SetFloat("_FaultTime",originalTime); }
        }

        private void CheckSceneDarkening(string label,float maximumRatio)
        {
            // The two renders share the same frame, enemies, glass and HUD. Disable
            // only the fault layer for a fair reference: sparse white glass and HUD
            // labels must not disguise a display that still fails to dim the world.
            var dimmed=CapturePixels();
            bool wasEnabled=faultGlass.enabled;
            Color32[] clear;
            try { faultGlass.enabled=false; clear=CapturePixels(); }
            finally { faultGlass.enabled=wasEnabled;Canvas.ForceUpdateCanvases(); }
            int width=Screen.width,height=Screen.height;
            var ratios=new List<float>();
            for(int y=height*3/10;y<height*7/10;y++)
            for(int x=width*3/10;x<width*7/10;x++)
            {
                int index=y*width+x;
                float reference=Luminance(clear[index]);
                if(reference>.08f)ratios.Add(Luminance(dimmed[index])/reference);
            }
            ratios.Sort();
            float median=ratios.Count>0?ratios[ratios.Count/2]:1f;
            Check(ratios.Count>1000 && median<maximumRatio && median>.1f,
                "scene_"+label+"_visible_darkening",
                $"medianDisplayBrightnessRatio={F(median)} maximum={F(maximumRatio)} scenePixels={ratios.Count} colorSpace={QualitySettings.activeColorSpace}");
            if(label=="health15")WritePixels("04c_Health15_FaultDisabledComparison.png",clear,width,height);
        }

        private static float Luminance(Color32 color) => (color.r*.2126f+color.g*.7152f+color.b*.0722f)/255f;
        private void WritePixels(string name,Color32[] data,int width,int height)
        {
            var texture=new Texture2D(width,height,TextureFormat.RGBA32,false);
            try
            {
                texture.SetPixels32(data);texture.Apply();
                File.WriteAllBytes(Path.Combine(output,name),texture.EncodeToPNG());
            }
            finally { Destroy(texture); }
        }

        private void Capture(string name)
        {
            WritePixels(name,CapturePixels(),Mathf.Max(1,Screen.width),Mathf.Max(1,Screen.height));
            string file=Path.Combine(output,name);
            results.Add($"SCREENSHOT {name} {Screen.width}x{Screen.height} bytes={new FileInfo(file).Length}");
        }

        private Color32[] CapturePixels()
        {
            var camera=view!=null?view:Camera.main;
            if(camera==null)throw new InvalidOperationException("Evidence camera is missing.");
            int width=Mathf.Max(1,Screen.width),height=Mathf.Max(1,Screen.height);
            var texture=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var previousTarget=camera.targetTexture;
            var previousActive=RenderTexture.active;
            Texture2D pixels=null;
            try
            {
                camera.targetTexture=texture;
                Canvas.ForceUpdateCanvases();
                camera.Render();
                RenderTexture.active=texture;
                pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();
                return pixels.GetPixels32();
            }
            finally
            {
                camera.targetTexture=previousTarget;
                RenderTexture.active=previousActive;
                if(pixels!=null)Destroy(pixels);
                texture.Release();Destroy(texture);
            }
        }
    }
}
