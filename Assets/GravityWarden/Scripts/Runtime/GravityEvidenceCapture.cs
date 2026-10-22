using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    public sealed class GravityEvidenceCapture : MonoBehaviour
    {
        public GravityGameManager game;
        private string outputRoot;
        private bool captureFailed;
        private Camera evidenceCamera;
        private Rect walkRoiViewport;
        private Rect pullRoiViewport;
        private readonly Dictionary<string, Color32[]> phaseReferencePixels = new();
        private readonly Dictionary<string, string> phaseReferenceStages = new();

        private void Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravityEvidence")) return;
            outputRoot = ReadArgument("-gravityEvidenceOutput") ?? Path.Combine(Application.persistentDataPath, "GravityEvidence");
            Directory.CreateDirectory(outputRoot);
            StartCoroutine(RunEvidence());
        }

        private IEnumerator RunEvidence()
        {
            yield return new WaitForSeconds(1f);
            game ??= FindFirstObjectByType<GravityGameManager>();
            if (game == null || game.AliveEnemies.Count == 0)
            {
                Debug.LogError("GRAVITY_EVIDENCE_FAIL manager_or_enemies_missing");
                Application.Quit(3);
                yield break;
            }

            StageCameraAndEnemies();
            yield return new WaitForSeconds(.35f);
            yield return CaptureHudHandsAndProps();
            if (captureFailed) yield break;
            yield return CaptureHealthPackFront();
            if (captureFailed) yield break;
            yield return CaptureWalkCloseup();
            if (captureFailed) yield break;
            yield return Capture("00_RobotFronts_Reference.png", "robot_fronts_reference");
            if (captureFailed) yield break;
            yield return Capture("00_Stars_Twinkle_A.png", "stars_twinkle_a");
            if (captureFailed) yield break;
            yield return new WaitForSeconds(1.25f);
            yield return Capture("00_Stars_Twinkle_B.png", "stars_twinkle_b");
            if (captureFailed) yield break;

            var projectileSources = game.AliveEnemies.Select(e => e.GetComponent<RobotEnemy>()).Where(e => e != null).ToArray();
            var projectile26 = projectileSources.Select(e => e.projectilePrefab).FirstOrDefault(p => p != null && p.name.Contains("26"));
            var projectile28 = projectileSources.SelectMany(e => new[] { e.projectilePrefab, e.alternateProjectilePrefab }).FirstOrDefault(p => p != null && p.name.Contains("28"));
            yield return CaptureIncomingProjectile(projectile26, "P0_26_Missile_NoseTowardCamera.png", "missile26_nose");
            if (captureFailed) yield break;
            yield return CaptureIncomingProjectile(projectile28, "P0_28_Missile_NoseTowardCamera.png", "missile28_nose");
            if (captureFailed) yield break;

            var pullEnemy = game.AliveEnemies.First();
            game.leftHand.DebugGrab(pullEnemy);
            game.leftHand.enabled = false;
            var pullCenter = game.playerHead.position + game.playerHead.forward * 4.1f - game.playerHead.right * 1.05f + Vector3.up * .28f;
            game.leftHand.transform.position = game.playerHead.position - game.playerHead.right * .72f - Vector3.up * .48f + game.playerHead.forward * .65f;
            game.leftHand.transform.rotation = Quaternion.LookRotation(pullCenter - game.leftHand.transform.position, Vector3.up);
            pullEnemy.transform.position += pullCenter - pullEnemy.Center;
            pullEnemy.transform.rotation = RobotEnemy.RootRotationFacing(game.playerHead.position - pullEnemy.transform.position);
            yield return new WaitForSeconds(.45f);
            var pullLogic = pullEnemy.GetComponent<RobotEnemy>();
            var pullToPlayer = Vector3.ProjectOnPlane(game.playerHead.position - pullEnemy.transform.position, Vector3.up).normalized;
            if (pullLogic == null || Vector3.Dot(pullLogic.VisualFacingDirection, pullToPlayer) < .95f || pullLogic.visualRoot == null || Mathf.Abs(Mathf.DeltaAngle(pullLogic.visualRoot.localEulerAngles.y, 180f)) > .1f)
            {
                FailCapture("pull_front_facing", Path.Combine(outputRoot, "01_GravityPull_Struggle_BeamRings.png"), "held_robot_not_front_facing");
                yield break;
            }
            Debug.Log($"GRAVITY_EVIDENCE_HELD_FRONT_PASS dot={Vector3.Dot(pullLogic.VisualFacingDirection, pullToPlayer):0.000} visualLocalEuler={pullLogic.visualRoot.localEulerAngles}");
            var pullRig = pullEnemy.GetComponent<RobotRigMotion>();
            pullRoiViewport = RendererViewportBounds(pullEnemy.GetComponentsInChildren<Renderer>(true), Camera.main, .025f);
            pullRig?.DebugSetPose(.25f, false, true);
            yield return Capture("01_GravityPull_Sway_A.png", "pull_sway_a");
            if (captureFailed) yield break;
            pullRig?.DebugSetPose(.75f, false, true);
            yield return Capture("01_GravityPull_Sway_B.png", "pull_sway_b");
            if (captureFailed) yield break;
            yield return Capture("01_GravityPull_Struggle_BeamRings.png", "pull");
            if (captureFailed) yield break;
            game.leftHand.ForceForget(pullEnemy);
            game.leftHand.enabled = true;
            pullEnemy.gameObject.SetActive(false);
            Destroy(pullEnemy.gameObject);
            pullRoiViewport = default;
            yield return new WaitForSeconds(.2f);

            var robot = game.AliveEnemies.First();
            var enemyLogic = robot.GetComponent<RobotEnemy>();
            if (enemyLogic?.projectilePrefab != null)
            {
                var position = game.playerHead.position + game.playerHead.forward * 4.2f + Vector3.up * .15f;
                var projectileGo = Instantiate(enemyLogic.projectilePrefab, position, Quaternion.identity);
                projectileGo.transform.localScale *= 1.65f;
                var projectile = projectileGo.GetComponent<EnemyProjectile>();
                projectile.Launch(-game.playerHead.forward);
                yield return new WaitForSeconds(.12f);
                game.Repulse();
                yield return new WaitForSeconds(.075f);
                yield return Capture("02_Repulse_MissileReflect_Shockwave.png", "repulse_reflect");
                if (captureFailed) yield break;
                projectileGo.SetActive(false);
                Destroy(projectileGo);
                yield return new WaitForSeconds(.68f);
            }

            var breakTarget = game.AliveEnemies.FirstOrDefault(e => e != robot) ?? game.AliveEnemies.FirstOrDefault();
            if (breakTarget != null)
            {
                var desiredCenter = game.playerHead.position + game.playerHead.forward * 6f - game.playerHead.right * 2.15f + Vector3.up * .35f;
                breakTarget.transform.position += desiredCenter - breakTarget.Center;
                breakTarget.DestroyTarget(DestroyReason.SmokeTest, false);
                yield return new WaitForSeconds(.08f);
                yield return Capture("P0_Death_CenterDissolve_01_Flash.png", "death_center_flash");
                if (captureFailed) yield break;
                yield return new WaitForSeconds(.18f);
                yield return Capture("P0_Death_CenterDissolve_02_Mid.png", "death_center_mid");
                if (captureFailed) yield break;
                yield return new WaitForSeconds(.22f);
                yield return Capture("P0_Death_CenterDissolve_03_End.png", "death_center_end");
                if (captureFailed) yield break;
                yield return new WaitForSeconds(1.55f);
            }

            game.DebugReadyUltimate();
            game.ActivateUltimate();
            yield return new WaitForSeconds(.55f);
            yield return Capture("04_Ultimate_Stage1_Attract.png", "ultimate_attract");
            if (captureFailed) yield break;
            yield return new WaitForSeconds(.75f);
            yield return Capture("05_Ultimate_Stage2_Orbit.png", "ultimate_orbit");
            if (captureFailed) yield break;
            yield return new WaitForSeconds(1.4f);
            yield return Capture("06_Ultimate_Stage3_Compress.png", "ultimate_compress");
            if (captureFailed) yield break;
            yield return new WaitForSeconds(1.75f);
            yield return Capture("07_Ultimate_Stage4_Blast.png", "ultimate_blast");
            if (captureFailed) yield break;
            yield return new WaitForSeconds(.7f);
            game.hud?.ShowRunSummary(GravityGameManager.BuildEvidenceRunSummary());
            var resultFailure = game.hud == null ? "hud_missing" : string.Empty;
            if (game.hud == null || !game.hud.ValidateResultLayout(Camera.main, out resultFailure))
            {
                FailCapture("run_summary", Path.Combine(outputRoot, "08_RunSummary_SideCards_Topmost.png"), "result_layout_" + resultFailure);
                yield break;
            }
            yield return Capture("08_RunSummary_SideCards_Topmost.png", "run_summary");
            if (captureFailed) yield break;
            Debug.Log("GRAVITY_EVIDENCE_PASS output=" + outputRoot);
            yield return new WaitForSeconds(.3f);
            Application.Quit(0);
        }

        private IEnumerator CaptureHudHandsAndProps()
        {
            if (Camera.main == null || game.playerHead == null || game.leftHand == null || game.rightHand == null)
            {
                FailCapture("world_start", Path.Combine(outputRoot, "00_WorldStart_ControllerRays.png"), "camera_head_or_hands_missing");
                yield break;
            }

            var leftEnabled = game.leftHand.enabled;
            var rightEnabled = game.rightHand.enabled;
            var leftActive = game.leftHand.gameObject.activeSelf;
            var rightActive = game.rightHand.gameObject.activeSelf;
            var hudActive = game.hud != null && game.hud.gameObject.activeSelf;
            var leftPosition = game.leftHand.transform.position;
            var rightPosition = game.rightHand.transform.position;
            var leftRotation = game.leftHand.transform.rotation;
            var rightRotation = game.rightHand.transform.rotation;
            var props = FindObjectsByType<GravityTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(t => t.kind == GravityTargetKind.Prop && t.HasValidVisibleInteractionGeometry(out _))
                .OrderBy(t => t.name)
                .ToArray();
            var selected = props.Where(prop => prop.MatchesApprovedPropIdentity())
                .GroupBy(prop => prop.displayName)
                .Select(group => group.First())
                .Take(3)
                .ToArray();
            if (selected.Length < 3)
            {
                FailCapture("prop_whitelist", Path.Combine(outputRoot, "00_Props_Whitelist_ExistingSceneModels.png"), "three_confirmed_existing_scene_models_missing");
                yield break;
            }

            var propStates = selected.Select(prop => new PropCaptureState(prop)).ToArray();
            var propActiveStates = props.ToDictionary(prop => prop, prop => prop.gameObject.activeSelf);
            var healthPacks = FindObjectsByType<GravityTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(target => target.kind == GravityTargetKind.Heal)
                .ToArray();
            var healthPackStates = healthPacks.Select(pack => pack.gameObject.activeSelf).ToArray();
            var evidenceLabels = new List<GameObject>();
            try
            {
                if (game.designStart == null || !game.designStart.MatchesRig() || game.hud == null || !game.hud.CameraHudConfigured)
                {
                    FailCapture("world_start", Path.Combine(outputRoot, "00_WorldStart_ControllerRays.png"), "design_start_or_camera_hud_invalid");
                    yield break;
                }
                game.leftHand.enabled = false;
                game.rightHand.enabled = false;
                var head = game.playerHead;
                game.leftHand.transform.position = head.position + head.forward * .88f - head.right * .43f - head.up * .33f;
                game.rightHand.transform.position = head.position + head.forward * .88f + head.right * .43f - head.up * .33f;
                game.leftHand.transform.rotation = Quaternion.LookRotation(head.forward + head.right * .06f, head.up);
                game.rightHand.transform.rotation = Quaternion.LookRotation(head.forward - head.right * .06f, head.up);
                game.hud?.Refresh(game);
                Physics.SyncTransforms();
                yield return Capture("00_WorldStart_ControllerRays.png", "world_start");
                if (captureFailed) yield break;

                game.leftHand.gameObject.SetActive(false);
                game.rightHand.gameObject.SetActive(false);
                if (game.hud != null) game.hud.gameObject.SetActive(false);
                foreach (var pack in healthPacks) pack.gameObject.SetActive(false);
                foreach (var prop in props.Where(prop => !selected.Contains(prop))) prop.gameObject.SetActive(false);

                for (var i = 0; i < selected.Length; i++)
                {
                    var prop = selected[i];
                    if (prop.Body != null)
                    {
                        prop.Body.linearVelocity = Vector3.zero;
                        prop.Body.angularVelocity = Vector3.zero;
                        prop.Body.isKinematic = true;
                    }
                    var desiredCenter = head.position + head.forward * 4.65f + head.right * ((i - 1) * 2.05f) - head.up * .62f;
                    prop.transform.position += desiredCenter - prop.Center;
                    prop.transform.rotation = Quaternion.Euler(0f, i == 1 ? 0f : 16f + i * 16f, 0f);
                }
                Physics.SyncTransforms();
                for (var i = 0; i < selected.Length; i++)
                    evidenceLabels.Add(CreateEvidenceLabel(selected[i].displayName, selected[i].Center + head.up * (selected[i].WorldBounds.extents.y + .38f), head));
                yield return Capture("00_Props_Whitelist_ExistingSceneModels.png", "prop_whitelist");
            }
            finally
            {
                foreach (var label in evidenceLabels) if (label != null) Destroy(label);
                for (var i = 0; i < healthPacks.Length; i++) healthPacks[i].gameObject.SetActive(healthPackStates[i]);
                foreach (var state in propActiveStates) if (state.Key != null) state.Key.gameObject.SetActive(state.Value);
                for (var i = 0; i < selected.Length; i++) propStates[i].Restore(selected[i]);
                game.leftHand.transform.SetPositionAndRotation(leftPosition, leftRotation);
                game.rightHand.transform.SetPositionAndRotation(rightPosition, rightRotation);
                game.leftHand.gameObject.SetActive(leftActive);
                game.rightHand.gameObject.SetActive(rightActive);
                if (game.hud != null) game.hud.gameObject.SetActive(hudActive);
                game.leftHand.enabled = leftEnabled;
                game.rightHand.enabled = rightEnabled;
                Physics.SyncTransforms();
            }
        }

        private IEnumerator CaptureHealthPackFront()
        {
            if (game.healthPackPrefab == null || game.playerHead == null)
            {
                FailCapture("health_pack_front", Path.Combine(outputRoot, "00_HealthPack27_FrontTowardCamera.png"), "health_prefab_or_player_missing");
                yield break;
            }

            var head = game.playerHead;
            var packObject = Instantiate(game.healthPackPrefab, head.position + head.forward * 4.1f - head.up * .72f, Quaternion.identity);
            var pack = packObject.GetComponent<GravityTarget>();
            var billboard = packObject.GetComponent<FacePlayerBillboard>();
            var leftActive = game.leftHand != null && game.leftHand.gameObject.activeSelf;
            var rightActive = game.rightHand != null && game.rightHand.gameObject.activeSelf;
            var hudActive = game.hud != null && game.hud.gameObject.activeSelf;
            try
            {
                if (game.leftHand != null) game.leftHand.gameObject.SetActive(false);
                if (game.rightHand != null) game.rightHand.gameObject.SetActive(false);
                if (game.hud != null) game.hud.gameObject.SetActive(false);
                yield return null;
                yield return new WaitForEndOfFrame();
                if (pack == null || billboard == null || billboard.PlayerFacingDot < .98f)
                {
                    FailCapture("health_pack_front", Path.Combine(outputRoot, "00_HealthPack27_FrontTowardCamera.png"), $"front_dot_{billboard?.PlayerFacingDot:0.000}");
                    yield break;
                }
                Debug.Log($"GRAVITY_HEALTH_EVIDENCE_FACING_PASS asset=27 dot={billboard.PlayerFacingDot:0.000} visual={pack.replaceableVisual?.name}");
                yield return Capture("00_HealthPack27_FrontTowardCamera.png", "health_pack_front");
            }
            finally
            {
                if (game.leftHand != null) game.leftHand.gameObject.SetActive(leftActive);
                if (game.rightHand != null) game.rightHand.gameObject.SetActive(rightActive);
                if (game.hud != null) game.hud.gameObject.SetActive(hudActive);
                if (packObject != null) Destroy(packObject);
            }
        }

        private static GameObject CreateEvidenceLabel(string value, Vector3 position, Transform viewer)
        {
            var label = new GameObject("EVIDENCE_LABEL_" + value);
            label.transform.position = position;
            label.transform.rotation = Quaternion.LookRotation(position - viewer.position, viewer.up);
            var text = label.AddComponent<TextMesh>();
            text.text = value;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.fontSize = 48;
            text.characterSize = .042f;
            text.color = new Color(1f, .9f, .55f, 1f);
            // 英文版去除 PingFang/中文字体回退，使用嵌入的 Arimo 字体
            var arimoEvidence = Resources.Load<Font>("GravityFonts/Arimo-Regular");
            var font = arimoEvidence != null
                ? arimoEvidence
                : Font.CreateDynamicFontFromOSFont(new[] { "Arial", "Helvetica" }, 64);
            text.font = font;
            text.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return label;
        }

        private IEnumerator CaptureWalkCloseup()
        {
            var camera = Camera.main;
            var enemies = game.AliveEnemies.Where(enemy => enemy != null).ToArray();
            var walkEnemy = enemies.FirstOrDefault(enemy => enemy.name.Contains("Engineer", StringComparison.OrdinalIgnoreCase)) ?? enemies.FirstOrDefault();
            if (camera == null || game.playerHead == null || walkEnemy == null)
            {
                FailCapture("walk_closeup", Path.Combine(outputRoot, "00_Walk_Phase_A.png"), "walk_camera_head_or_robot_missing");
                yield break;
            }

            var enemyActiveStates = enemies.Select(enemy => enemy.gameObject.activeSelf).ToArray();
            var walkPosition = walkEnemy.transform.position;
            var walkRotation = walkEnemy.transform.rotation;
            var leftActive = game.leftHand != null && game.leftHand.gameObject.activeSelf;
            var rightActive = game.rightHand != null && game.rightHand.gameObject.activeSelf;
            var hudActive = game.hud != null && game.hud.gameObject.activeSelf;
            var nightSky = FindFirstObjectByType<GravityNightSky>();
            var starsEnabled = nightSky != null && nightSky.stars != null && nightSky.stars.enabled;
            try
            {
                foreach (var enemy in enemies) enemy.gameObject.SetActive(enemy == walkEnemy);
                if (game.leftHand != null) game.leftHand.gameObject.SetActive(false);
                if (game.rightHand != null) game.rightHand.gameObject.SetActive(false);
                if (game.hud != null) game.hud.gameObject.SetActive(false);
                if (nightSky != null && nightSky.stars != null) nightSky.stars.enabled = false;

                var desiredCenter = game.playerHead.position + game.playerHead.forward * 3.65f - game.playerHead.up * .2f;
                walkEnemy.transform.position += desiredCenter - walkEnemy.Center;
                walkEnemy.transform.rotation = RobotEnemy.RootRotationFacing(game.playerHead.position - walkEnemy.transform.position);
                Physics.SyncTransforms();
                walkRoiViewport = RendererViewportBounds(walkEnemy.GetComponentsInChildren<Renderer>(true), camera, .025f);
                if (walkRoiViewport.height < .35f || walkRoiViewport.width < .12f)
                {
                    FailCapture("walk_closeup", Path.Combine(outputRoot, "00_Walk_Phase_A.png"), $"robot_too_small viewport={walkRoiViewport}");
                    yield break;
                }

                var rig = walkEnemy.GetComponent<RobotRigMotion>();
                if (rig == null || !rig.HasRig)
                {
                    FailCapture("walk_closeup", Path.Combine(outputRoot, "00_Walk_Phase_A.png"), "walk_rig_missing");
                    yield break;
                }
                rig.DebugSetPose(.25f, true, false);
                yield return Capture("00_Walk_Phase_A.png", "walk_phase_a");
                if (captureFailed) yield break;
                rig.DebugSetPose(.75f, true, false);
                yield return Capture("00_Walk_Phase_B.png", "walk_phase_b");
            }
            finally
            {
                walkEnemy.transform.SetPositionAndRotation(walkPosition, walkRotation);
                walkEnemy.GetComponent<RobotRigMotion>()?.DebugSetPose(0f, false, false);
                for (var i = 0; i < enemies.Length; i++) enemies[i].gameObject.SetActive(enemyActiveStates[i]);
                if (game.leftHand != null) game.leftHand.gameObject.SetActive(leftActive);
                if (game.rightHand != null) game.rightHand.gameObject.SetActive(rightActive);
                if (game.hud != null) game.hud.gameObject.SetActive(hudActive);
                if (nightSky != null && nightSky.stars != null) nightSky.stars.enabled = starsEnabled;
                walkRoiViewport = default;
                Physics.SyncTransforms();
            }
        }

        private IEnumerator CaptureIncomingProjectile(GameObject prefab, string filename, string stage)
        {
            if (prefab == null)
            {
                FailCapture(stage, Path.Combine(outputRoot, filename), "projectile_prefab_missing");
                yield break;
            }
            var position = game.playerHead.position + game.playerHead.forward * 4.8f + Vector3.up * .35f;
            var projectileGo = Instantiate(prefab, position, Quaternion.identity);
            var projectile = projectileGo.GetComponent<EnemyProjectile>();
            projectile.speed = .35f;
            projectile.Launch((game.playerHead.position - position).normalized);
            yield return new WaitForSeconds(.08f);
            if (projectile.CameraNoseDot < .98f)
            {
                FailCapture(stage, Path.Combine(outputRoot, filename), $"camera_nose_dot_{projectile.CameraNoseDot:0.000}");
                yield break;
            }
            yield return Capture(filename, stage);
            projectileGo.SetActive(false);
            Destroy(projectileGo);
            yield return new WaitForSeconds(.12f);
        }

        private void StageCameraAndEnemies()
        {
            var camera = Camera.main;
            if (camera == null || game.playerHead == null) return;
            var previous = game.AliveEnemies.ToArray();
            foreach (var enemy in previous)
            {
                if (enemy == null) continue;
                enemy.gameObject.SetActive(false);
                Destroy(enemy.gameObject);
            }
            var prefabs = new[] { game.scoutRobotPrefab, game.guardRobotPrefab, game.engineerRobotPrefab, game.scoutRobotPrefab, game.engineerRobotPrefab, game.scoutRobotPrefab };
            var positions = new[]
            {
                new Vector3(-3.8f, 0f, -.85f), new Vector3(0f, 0f, -2.1f), new Vector3(3.8f, 0f, -.85f),
                new Vector3(5.2f, 0f, -5.8f), new Vector3(-5.2f, 0f, -6.2f), new Vector3(0f, 0f, -7.4f)
            };
            for (var i = 0; i < prefabs.Length; i++)
            {
                if (prefabs[i] == null) continue;
                var go = Instantiate(prefabs[i], positions[i], RobotEnemy.RootRotationFacing(game.playerHead.position - positions[i]));
                var logic = go.GetComponent<RobotEnemy>();
                if (logic != null) logic.enabled = false;
            }
            Physics.SyncTransforms();
        }

        private IEnumerator Capture(string filename, string stage)
        {
            yield return new WaitForEndOfFrame();
            var path = Path.Combine(outputRoot, filename);
            var sourceCamera = Camera.main;
            if (sourceCamera == null)
            {
                FailCapture(stage, path, "main_camera_missing");
                yield break;
            }

            var previousActive = RenderTexture.active;
            var renderTarget = RenderTexture.GetTemporary(1600, 1000, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGBA32, false);
            try
            {
                var hudCanvas = game != null && game.hud != null ? game.hud.GetComponent<Canvas>() : null;
                var previousHudCamera = hudCanvas != null ? hudCanvas.worldCamera : null;
                if (hudCanvas != null && hudCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                    hudCanvas.worldCamera = evidenceCamera;
                PrepareEvidenceCamera(sourceCamera, renderTarget);
                if (hudCanvas != null && hudCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                    hudCanvas.worldCamera = evidenceCamera;
                Canvas.ForceUpdateCanvases();
                Graphics.SetRenderTarget(renderTarget);
                GL.Clear(true, true, evidenceCamera.backgroundColor);
                evidenceCamera.Render();
                RenderTexture.active = renderTarget;
                image.ReadPixels(new Rect(0, 0, renderTarget.width, renderTarget.height), 0, 0);
                image.Apply(false, false);
                var pixels = image.GetPixels32();
                if (!ValidateContent(stage, pixels, out var statistics, out var validationFailure))
                {
                    FailCapture(stage, path, validationFailure + " " + statistics);
                }
                else if (!ValidatePhaseDifference(stage, pixels, out var phaseStatistics, out var phaseFailure))
                {
                    FailCapture(stage, path, phaseFailure + " " + phaseStatistics);
                }
                else
                {
                    var png = image.EncodeToPNG();
                    var sha256 = ComputeSha256(png);
                    File.WriteAllBytes(path, png);
                    Debug.Log($"GRAVITY_EVIDENCE_PIXELS_PASS stage={stage} {statistics} {phaseStatistics} sha256={sha256}");
                }
            }
            catch (Exception exception)
            {
                FailCapture(stage, path, exception.GetType().Name + ":" + exception.Message);
            }
            finally
            {
                var hudCanvas = game != null && game.hud != null ? game.hud.GetComponent<Canvas>() : null;
                if (hudCanvas != null && hudCanvas.renderMode == RenderMode.ScreenSpaceCamera)
                    hudCanvas.worldCamera = Camera.main;
                if (evidenceCamera != null) evidenceCamera.targetTexture = null;
                RenderTexture.active = previousActive;
                RenderTexture.ReleaseTemporary(renderTarget);
                Destroy(image);
            }

            if (captureFailed) yield break;
            yield return null;
            var file = new FileInfo(path);
            if (!file.Exists || file.Length < 10000)
            {
                FailCapture(stage, path, "png_missing_or_too_small");
                yield break;
            }
            Debug.Log($"GRAVITY_EVIDENCE_CAPTURE stage={stage} bytes={file.Length} path={path}");
        }

        private void PrepareEvidenceCamera(Camera source, RenderTexture target)
        {
            if (evidenceCamera == null)
            {
                var cameraObject = new GameObject("Evidence Camera (Non-XR)");
                cameraObject.hideFlags = HideFlags.HideAndDontSave;
                evidenceCamera = cameraObject.AddComponent<Camera>();
            }
            evidenceCamera.CopyFrom(source);
            evidenceCamera.transform.SetPositionAndRotation(source.transform.position, source.transform.rotation);
            evidenceCamera.stereoTargetEye = StereoTargetEyeMask.None;
            evidenceCamera.enabled = false;
            evidenceCamera.targetTexture = target;
            evidenceCamera.rect = new Rect(0f, 0f, 1f, 1f);
            evidenceCamera.ResetStereoProjectionMatrices();
            evidenceCamera.ResetStereoViewMatrices();
        }

        private static bool ValidateContent(string stage, Color32[] pixels, out string statistics, out string failure)
        {
            failure = string.Empty;
            if (pixels == null || pixels.Length == 0)
            {
                statistics = "pixels=0";
                failure = "empty_pixel_buffer";
                return false;
            }
            double sum = 0d;
            double sumSquared = 0d;
            byte minimum = byte.MaxValue;
            byte maximum = byte.MinValue;
            for (var i = 0; i < pixels.Length; i++)
            {
                var pixel = pixels[i];
                var luminance = (byte)Mathf.Clamp(Mathf.RoundToInt(pixel.r * .2126f + pixel.g * .7152f + pixel.b * .0722f), 0, 255);
                sum += luminance;
                sumSquared += luminance * luminance;
                if (luminance < minimum) minimum = luminance;
                if (luminance > maximum) maximum = luminance;
            }
            var mean = sum / pixels.Length;
            var variance = Math.Max(0d, sumSquared / pixels.Length - mean * mean);
            var standardDeviation = Math.Sqrt(variance);
            var range = maximum - minimum;
            statistics = $"mean={mean:0.00} stddev={standardDeviation:0.00} min={minimum} max={maximum} range={range}";
            if (standardDeviation < 5.5d || range < 38)
            {
                failure = "flat_gray_black_or_low_range";
                return false;
            }
            return true;
        }

        private bool ValidatePhaseDifference(string stage, Color32[] pixels, out string statistics, out string failure)
        {
            statistics = "phaseDiff=not_required";
            failure = string.Empty;
            var group = PhaseGroup(stage, out var compareNow, out var keepCurrent);
            if (string.IsNullOrEmpty(group)) return true;
            var comparisonPixels = pixels;
            var regionStatistics = string.Empty;
            if (group == "walk" || group == "pull_sway")
            {
                var roi = group == "walk" ? walkRoiViewport : pullRoiViewport;
                if (roi.width <= 0f || roi.height <= 0f)
                {
                    statistics = group + "Roi=missing";
                    failure = group + "_roi_missing";
                    return false;
                }
                comparisonPixels = CropPixels(pixels, 1600, 1000, roi, out var pixelRect);
                regionStatistics = $" {group}Roi={pixelRect.x},{pixelRect.y},{pixelRect.width},{pixelRect.height}";
            }
            if (!phaseReferencePixels.TryGetValue(group, out var reference))
            {
                phaseReferencePixels[group] = (Color32[])comparisonPixels.Clone();
                phaseReferenceStages[group] = stage;
                statistics = $"phaseDiff=reference group={group}{regionStatistics}";
                return true;
            }
            if (!compareNow) return true;
            long absoluteDelta = 0;
            var changedPixels = 0;
            var count = Math.Min(reference.Length, comparisonPixels.Length);
            for (var i = 0; i < count; i++)
            {
                var delta = Math.Abs(reference[i].r - comparisonPixels[i].r) + Math.Abs(reference[i].g - comparisonPixels[i].g) + Math.Abs(reference[i].b - comparisonPixels[i].b);
                absoluteDelta += delta;
                if (delta >= 12) changedPixels++;
            }
            var meanAbsolute = count > 0 ? absoluteDelta / (double)(count * 3 * 255) : 0d;
            var changedFraction = count > 0 ? changedPixels / (double)count : 0d;
            statistics = $"phaseDiffGroup={group} reference={phaseReferenceStages[group]} meanAbs={meanAbsolute:0.000000} changedFraction={changedFraction:0.000000}{regionStatistics}";
            var minimumMeanAbsolute = group == "stars" ? .000001d : .00012d;
            var minimumChangedFraction = group == "stars" ? .00002d : .0007d;
            statistics += $" thresholds={minimumMeanAbsolute:0.000000}/{minimumChangedFraction:0.000000}";
            if (meanAbsolute < minimumMeanAbsolute && changedFraction < minimumChangedFraction)
            {
                failure = "phase_images_insufficiently_different";
                return false;
            }
            if (keepCurrent)
            {
                phaseReferencePixels[group] = (Color32[])comparisonPixels.Clone();
                phaseReferenceStages[group] = stage;
            }
            else
            {
                phaseReferencePixels.Remove(group);
                phaseReferenceStages.Remove(group);
            }
            return true;
        }

        private static Rect RendererViewportBounds(IEnumerable<Renderer> renderers, Camera camera, float padding)
        {
            var found = false;
            var minimum = Vector2.one * float.PositiveInfinity;
            var maximum = Vector2.one * float.NegativeInfinity;
            foreach (var renderer in renderers.Where(item => item != null && item.enabled && item.gameObject.activeInHierarchy))
            {
                var bounds = renderer.bounds;
                var center = bounds.center;
                var extents = bounds.extents;
                for (var x = -1; x <= 1; x += 2)
                for (var y = -1; y <= 1; y += 2)
                for (var z = -1; z <= 1; z += 2)
                {
                    var viewport = camera.WorldToViewportPoint(center + Vector3.Scale(extents, new Vector3(x, y, z)));
                    if (viewport.z <= 0f) continue;
                    found = true;
                    minimum = Vector2.Min(minimum, viewport);
                    maximum = Vector2.Max(maximum, viewport);
                }
            }
            if (!found) return default;
            return Rect.MinMaxRect(
                Mathf.Clamp01(minimum.x - padding), Mathf.Clamp01(minimum.y - padding),
                Mathf.Clamp01(maximum.x + padding), Mathf.Clamp01(maximum.y + padding));
        }

        private static Color32[] CropPixels(Color32[] source, int sourceWidth, int sourceHeight, Rect viewport, out RectInt pixelRect)
        {
            var xMin = Mathf.Clamp(Mathf.FloorToInt(viewport.xMin * sourceWidth), 0, sourceWidth - 1);
            var xMax = Mathf.Clamp(Mathf.CeilToInt(viewport.xMax * sourceWidth), xMin + 1, sourceWidth);
            var yMin = Mathf.Clamp(Mathf.FloorToInt(viewport.yMin * sourceHeight), 0, sourceHeight - 1);
            var yMax = Mathf.Clamp(Mathf.CeilToInt(viewport.yMax * sourceHeight), yMin + 1, sourceHeight);
            pixelRect = new RectInt(xMin, yMin, xMax - xMin, yMax - yMin);
            var result = new Color32[pixelRect.width * pixelRect.height];
            for (var y = 0; y < pixelRect.height; y++)
                Array.Copy(source, (pixelRect.y + y) * sourceWidth + pixelRect.x, result, y * pixelRect.width, pixelRect.width);
            return result;
        }

        private static string PhaseGroup(string stage, out bool compareNow, out bool keepCurrent)
        {
            compareNow = false;
            keepCurrent = false;
            switch (stage)
            {
                case "walk_phase_a": return "walk";
                case "walk_phase_b": compareNow = true; return "walk";
                case "stars_twinkle_a": return "stars";
                case "stars_twinkle_b": compareNow = true; return "stars";
                case "pull_sway_a": return "pull_sway";
                case "pull_sway_b": compareNow = true; return "pull_sway";
                case "death_center_flash": return "death";
                case "death_center_mid": compareNow = true; keepCurrent = true; return "death";
                case "death_center_end": compareNow = true; return "death";
                case "ultimate_attract": return "ultimate";
                case "ultimate_orbit":
                case "ultimate_compress": compareNow = true; keepCurrent = true; return "ultimate";
                case "ultimate_blast": compareNow = true; return "ultimate";
                default: return null;
            }
        }

        private static string ComputeSha256(byte[] bytes)
        {
            using var algorithm = SHA256.Create();
            return string.Concat(algorithm.ComputeHash(bytes).Select(value => value.ToString("x2")));
        }

        private readonly struct CanvasCameraState
        {
            public readonly Canvas Canvas;
            private readonly Camera originalCamera;

            public CanvasCameraState(Canvas canvas)
            {
                Canvas = canvas;
                originalCamera = canvas.worldCamera;
            }

            public void Restore()
            {
                if (Canvas != null) Canvas.worldCamera = originalCamera;
            }
        }

        private readonly struct PropCaptureState
        {
            private readonly Vector3 position;
            private readonly Quaternion rotation;
            private readonly bool isKinematic;
            private readonly bool useGravity;

            public PropCaptureState(GravityTarget prop)
            {
                position = prop.transform.position;
                rotation = prop.transform.rotation;
                isKinematic = prop.Body != null && prop.Body.isKinematic;
                useGravity = prop.Body != null && prop.Body.useGravity;
            }

            public void Restore(GravityTarget prop)
            {
                if (prop == null) return;
                prop.transform.SetPositionAndRotation(position, rotation);
                if (prop.Body == null) return;
                prop.Body.isKinematic = isKinematic;
                prop.Body.useGravity = useGravity;
                prop.Body.linearVelocity = Vector3.zero;
                prop.Body.angularVelocity = Vector3.zero;
            }
        }

        private void FailCapture(string stage, string path, string reason)
        {
            captureFailed = true;
            Debug.LogError($"GRAVITY_EVIDENCE_FAIL stage={stage} reason={reason} path={path}");
            Application.Quit(4);
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
