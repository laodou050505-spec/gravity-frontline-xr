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
    /// <summary>Opt-in player evidence. No objects or behaviour are added during ordinary play.</summary>
    public sealed class GravityAimEvidence : MonoBehaviour
    {
        private readonly List<string> results = new();
        private readonly List<UnityEngine.Object> probes = new();
        private string output;
        private int passed, failed;
        private GravityGameManager game;
        private GravityVisorHud visor;
        private Camera view;
        private Transform head;
        private GravityHand left, right;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravityAimEvidence")) return;
            if (FindFirstObjectByType<GravityAimEvidence>() != null) return;
            var instance = new GameObject("EVIDENCE_ONLY_AimReticle0909");
            DontDestroyOnLoad(instance);
            instance.AddComponent<GravityAimEvidence>();
        }

        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "-gravityAimOutput");
            output = index >= 0 && index + 1 < args.Length ? args[index + 1]
                : Path.GetFullPath(Path.Combine("Evidence", "AimReticle0909"));
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
            string summary = $"GRAVITY_AIM_EVIDENCE_{(failed == 0 ? "PASS" : "FAIL")} passed={passed} failed={failed} output={output}";
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
            while (title != null && (!title.IsReadyForButtonInput || (titleHud != null && !titleHud.IsSpatialReady)) && Time.unscaledTime < deadline)
                yield return null;
            if (!Check(title != null && title.IsReadyForButtonInput, "title_ready", $"title={title != null} ready={title != null && title.IsReadyForButtonInput}")) yield break;
            Capture("00_Title.png");
            title.StartGame();
            yield return new WaitForSecondsRealtime(3f);
            game = FindFirstObjectByType<GravityGameManager>();
            view = Camera.main;
            if (!Check(game != null && game.hud != null && view != null, "game_started", $"game={game != null} camera={view != null}")) yield break;
            visor = game.hud.GetComponent<GravityVisorHud>();
            left = game.leftHand;
            right = game.rightHand;
            head = view.transform;
            if (!Check(visor != null && visor.Ready && left != null && right != null, "aim_components", $"visor={visor != null} left={left != null} right={right != null}")) yield break;
            game.hud.Refresh(game);
            Capture("01_CombatBaseline.png");

            // Freeze the live scene and move only this test's viewer/controllers to empty space.
            // All selection and HUD calls below still use the production physics/UI implementation.
            Time.timeScale = 0f;
            game.enabled = false;
            left.enabled = false;
            right.enabled = false;
            foreach (var desktop in FindObjectsByType<DesktopFallbackController>(FindObjectsSortMode.None)) desktop.enabled = false;
            foreach (var component in head.GetComponents<Behaviour>())
                if (component.GetType().Name.Contains("TrackedPoseDriver")) component.enabled = false;
            head.SetPositionAndRotation(new Vector3(1000f, 1000f, 1000f), Quaternion.identity);
            Pose(left, new Vector3(-.35f, -.18f, .4f), Vector3.forward);
            Pose(right, new Vector3(.35f, -.18f, .4f), Vector3.forward);
            Sync();
            right.TryGrab(); // A real empty trigger selects the right hand without acquiring anything.
            yield return null;
            Refresh();
            Check(visor.AimHand == right && !visor.AimLocked, "empty_trigger_selects_right", State());

            var centre = PointOnScreen();
            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(-.3f, 0, 1));
            Sync(); Refresh();
            var towardLeft = PointOnScreen();
            Check(towardLeft.x < centre.x - 40f && !visor.AimLocked && visor.AimReticle.gameObject.activeSelf,
                "aim_left", $"centre={V(centre)} actual={V(towardLeft)} {State()}");
            CheckProjection("aim_left_projection", right.AimPoint);
            Capture("02_AimLeft.png");
            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(.3f, 0, 1));
            Sync(); Refresh();
            var towardRight = PointOnScreen();
            Check(towardRight.x > centre.x + 40f && !visor.AimLocked && visor.AimReticle.gameObject.activeSelf,
                "aim_right", $"centre={V(centre)} actual={V(towardRight)} {State()}");
            CheckProjection("aim_right_projection", right.AimPoint);
            Capture("03_AimRight.png");
            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(0, .23f, 1));
            Sync(); Refresh();
            var towardUp = PointOnScreen();
            Check(towardUp.y > centre.y + 30f && !visor.AimLocked, "aim_up", $"centre={V(centre)} actual={V(towardUp)} {State()}");
            CheckProjection("aim_up_projection", right.AimPoint);
            Capture("04_AimUp.png");
            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(0, -.23f, 1));
            Sync(); Refresh();
            var towardDown = PointOnScreen();
            Check(towardDown.y < centre.y - 30f && !visor.AimLocked, "aim_down", $"centre={V(centre)} actual={V(towardDown)} {State()}");
            CheckProjection("aim_down_projection", right.AimPoint);
            Capture("05_AimDown.png");

            var target = MakeTarget("EVIDENCE_ONLY_RightTarget", head.TransformPoint(new Vector3(1.05f, .1f, 5f)));
            AimAt(right, target.Center);
            Sync(); Refresh();
            Check(right.FindAimTarget() == target && right.Candidate == target && visor.AimHand == right && visor.AimLocked,
                "real_target_lock", $"query={Name(right.FindAimTarget())} expected={target.name} {State()}");
            CheckProjection("target_centre_projection", target.Center);
            Capture("06_RealTargetLock.png");

            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(-.45f, 0, 1));
            Sync(); Refresh();
            float missAngle = Vector3.Angle(right.AimRay.direction, target.Center - right.AimRay.origin);
            Check(right.FindAimTarget() == null && right.Candidate == null && !visor.AimLocked,
                "off_target_no_lock", $"targetAngle={F(missAngle)} query={Name(right.FindAimTarget())} {State()}");
            CheckProjection("off_target_projection", right.AimPoint);
            Capture("07_OffTarget.png");

            AimAt(right, target.Center);
            var wallPosition = Vector3.Lerp(right.RayOriginPosition, target.Center, .52f);
            var wall = MakeCube("EVIDENCE_ONLY_NonTargetWall", wallPosition, new Vector3(1.6f, 1.6f, .18f), new Color(.34f, .16f, .06f));
            wall.transform.rotation = Quaternion.LookRotation(right.AimRay.direction);
            Sync(); Refresh();
            bool wallHit = Physics.Raycast(right.AimRay, out var wallHitInfo, right.rayLength, right.targetMask, QueryTriggerInteraction.Ignore) && wallHitInfo.collider.gameObject == wall;
            Check(wallHit && right.FindAimTarget() == null && right.Candidate == null && !visor.AimLocked,
                "wall_blocks_target", $"wallHit={wallHit} hitDistance={F(wallHitInfo.distance)} targetDistance={F(Vector3.Distance(right.RayOriginPosition, target.Center))} {State()}");
            CheckProjection("wall_hit_projection", right.AimPoint);
            Capture("08_WallBlocksTarget.png");
            wall.SetActive(false);
            Sync(); Refresh();
            Check(right.FindAimTarget() == target && visor.AimLocked, "wall_removed_restores_lock", State());

            // An idle hand hovering a valid target must not hijack an explicitly selected hand.
            var leftTarget = MakeTarget("EVIDENCE_ONLY_LeftTarget", head.TransformPoint(new Vector3(-1.05f, .1f, 5f)));
            AimAt(left, leftTarget.Center);
            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(.42f, 0, 1));
            Sync(); Refresh();
            right.TryGrab(); Refresh();
            Check(left.Candidate == leftTarget && visor.AimHand == right && !visor.AimLocked,
                "inactive_hover_does_not_steal", $"leftCandidate={Name(left.Candidate)} {State()}");
            Capture("09_InactiveHover.png");

            // Allow the documented explicit-input hold to expire, then exercise motion selection.
            yield return new WaitForSecondsRealtime(.9f);
            left.SendMessage("UpdateVelocity"); Refresh();
            Check(visor.AimHand == left && visor.AimLocked, "left_motion_selects_left", InputState());
            CheckProjection("left_target_projection", leftTarget.Center);
            Capture("10_LeftHandSelected.png");
            float jitter = 0, maxFrameProjectionError = 0;
            int visibleFrames = 0, stableHandFrames = 0;
            var stablePoint = PointOnScreen();
            for (int i = 0; i < 8; i++)
            {
                yield return null;
                yield return new WaitForEndOfFrame();
                // Observe the completed production frame without refreshing the HUD or
                // forcing Canvas layout: otherwise a LateUpdate ordering bug is masked.
                var actual = PointOnScreen();
                var expected = view.WorldToScreenPoint(leftTarget.Center);
                float frameError = Vector2.Distance(actual, new Vector2(expected.x, expected.y));
                jitter = Mathf.Max(jitter, Vector2.Distance(stablePoint, actual));
                maxFrameProjectionError = Mathf.Max(maxFrameProjectionError, frameError);
                if (visor.AimReticle.gameObject.activeInHierarchy && expected.z > 0) visibleFrames++;
                if (visor.AimHand == left && visor.AimLocked) stableHandFrames++;
            }
            Check(visibleFrames == 8 && stableHandFrames == 8 && jitter < .75f && maxFrameProjectionError < 2f,
                "idle_hand_selection_stable", $"frames=8 visibleFrames={visibleFrames} stableHandFrames={stableHandFrames} maxJitterPx={F(jitter)} maxProjectionErrorPx={F(maxFrameProjectionError)} observed=endOfFrame forcedRefresh=false {InputState()}");
            yield return new WaitForSecondsRealtime(.12f);
            right.SendMessage("UpdateVelocity"); Refresh();
            Check(visor.AimHand == right && !visor.AimLocked, "right_motion_selects_right", InputState());
            Capture("11_RightHandSelected.png");

            // Exercise explicit grab ownership with the real target request/release path.
            AimAt(left, leftTarget.Center);
            Sync();
            left.TryGrab(); Refresh();
            Check(left.Held == leftTarget && visor.AimHand == left && visor.AimLocked,
                "left_grab_selects_left", $"held={Name(left.Held)} {InputState()}");
            Capture("12_LeftGrabSelected.png");
            left.ReleaseHeld(0f);
            leftTarget.Body.isKinematic = true;
            leftTarget.gameObject.SetActive(false);
            target.gameObject.SetActive(false);
            Sync(); Refresh();
            yield return null;
            right.TryGrab(); Refresh();
            Check(visor.AimHand == right && !visor.AimLocked, "right_empty_trigger_switches", InputState());

            var oldReticle = visor.AimReticle;
            int before = visor.GetComponentsInChildren<RectTransform>(true).Length;
            visor.Configure(game.hud);
            visor.Configure(game.hud);
            yield return null;
            Refresh();
            int after = visor.GetComponentsInChildren<RectTransform>(true).Length;
            var layers = visor.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "VISOR_TOPMOST_CANVAS").ToArray();
            var reticles = visor.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "AIM_LOCK_RETICLE").ToArray();
            var dots = visor.GetComponentsInChildren<RectTransform>(true).Where(t => t.name == "AIM_LOCK_DOT").ToArray();
            var duplicateChildren = visor.AimReticle.Cast<Transform>().GroupBy(t => t.name).Where(g => g.Count() > 1).Select(g => g.Key).ToArray();
            Check(before == after && visor.AimReticle == oldReticle && layers.Length == 1 && reticles.Length == 1 && dots.Length == 1 && duplicateChildren.Length == 0,
                "configure_twice_reuses_elements", $"before={before} after={after} layers={layers.Length} reticles={reticles.Length} dots={dots.Length} duplicateChildren={string.Join(",", duplicateChildren)}");
            bool valid = visor.Validate(out var validation);
            Check(valid, "overlay_material_validation", $"valid={valid} reason={validation} sortingOrder={game.hud.GetComponent<Canvas>().sortingOrder}");
            Pose(right, new Vector3(.35f, -.18f, .4f), Vector3.forward);
            Sync(); Refresh();
            var occluder = MakeCube("EVIDENCE_ONLY_HudOccluder", head.TransformPoint(new Vector3(0, 0, .55f)), new Vector3(5f, 5f, .05f), new Color(.23f, .12f, .045f));
            Destroy(occluder.GetComponent<Collider>());
            yield return null;
            Refresh();
            Capture("13_TopmostOcclusion.png");
            Check(visor.Validate(out validation) && visor.AimReticle.gameObject.activeInHierarchy,
                "foreground_occlusion_overlay", $"foregroundDistance=.55 sortingOrder={game.hud.GetComponent<Canvas>().sortingOrder} reticleVisible={visor.AimReticle.gameObject.activeInHierarchy} reason={validation}");
            occluder.SetActive(false);
            Pose(right, new Vector3(.35f, -.18f, .4f), Vector3.back);
            Sync(); Refresh();
            Check(!visor.AimReticle.gameObject.activeSelf && !visor.AimLocked, "behind_eye_hidden", State());
            Pose(right, new Vector3(.35f, -.18f, .4f), new Vector3(5f, 0, 1));
            Sync(); Refresh();
            Check(!visor.AimReticle.gameObject.activeSelf && !visor.AimLocked, "offscreen_hidden", State());
        }

        private void Pose(GravityHand hand, Vector3 localPosition, Vector3 localDirection)
        {
            var rotation = Quaternion.LookRotation(head.TransformDirection(localDirection.normalized), head.up);
            hand.transform.SetPositionAndRotation(head.TransformPoint(localPosition), rotation);
            if (hand.rayOrigin != null) hand.rayOrigin.rotation = rotation;
        }

        private void AimAt(GravityHand hand, Vector3 point)
        {
            hand.transform.rotation = Quaternion.LookRotation(point - hand.RayOriginPosition, head.up);
            if (hand.rayOrigin != null)
                hand.rayOrigin.rotation = Quaternion.LookRotation(point - hand.RayOriginPosition, head.up);
        }

        private static void Sync() => Physics.SyncTransforms();

        private void Refresh()
        {
            left.SendMessage("UpdateFeedback");
            right.SendMessage("UpdateFeedback");
            Canvas.ForceUpdateCanvases();
            visor.RefreshAim(game);
            Canvas.ForceUpdateCanvases();
        }

        private GravityTarget MakeTarget(string name, Vector3 position)
        {
            var probe = MakeCube(name, position, Vector3.one * .65f, new Color(.25f, .85f, .56f));
            probe.SetActive(false);
            var body = probe.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            var target = probe.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Robot;
            target.displayName = name;
            target.pullStagesRequired = 1;
            probe.SetActive(true);
            target.RefreshGeometryCache();
            Sync();
            return target;
        }

        private GameObject MakeCube(string name, Vector3 position, Vector3 scale, Color color)
        {
            var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.position = position;
            cube.transform.localScale = scale;
            var shader = Shader.Find("Unlit/Color") ?? Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader);
            material.color = color;
            cube.GetComponent<Renderer>().sharedMaterial = material;
            probes.Add(cube);
            probes.Add(material);
            return cube;
        }

        private Vector2 PointOnScreen() => RectTransformUtility.WorldToScreenPoint(view, visor.AimReticle.position);

        private void CheckProjection(string name, Vector3 point)
        {
            var expected3 = view.WorldToScreenPoint(point);
            var expected = new Vector2(expected3.x, expected3.y);
            var actual = PointOnScreen();
            float error = Vector2.Distance(expected, actual);
            Check(expected3.z > 0 && visor.AimReticle.gameObject.activeInHierarchy && error < 2f, name,
                $"expectedPx={V(expected)} actualPx={V(actual)} errorPx={F(error)} point={point.ToString("F3", CultureInfo.InvariantCulture)}");
        }

        private string State() => $"hand={(visor.AimHand != null ? visor.AimHand.side.ToString() : "none")} locked={visor.AimLocked} visible={visor.AimReticle.gameObject.activeSelf} candidate={Name(visor.AimHand != null ? visor.AimHand.Candidate : null)}";
        private string InputState() => $"{State()} leftAim={F(left.LastAimInputTime)} rightAim={F(right.LastAimInputTime)} leftGrab={F(left.LastGrabInputTime)} rightGrab={F(right.LastGrabInputTime)}";
        private static string Name(UnityEngine.Object item) => item != null ? item.name : "none";
        private static string F(float value) => value.ToString("F3", CultureInfo.InvariantCulture);
        private static string V(Vector2 value) => $"({F(value.x)},{F(value.y)})";

        private bool Check(bool condition, string scenario, string detail)
        {
            if (condition) passed++; else failed++;
            string message = $"GRAVITY_AIM_CASE_{(condition ? "PASS" : "FAIL")} {scenario} {detail}";
            results.Add(message);
            if (condition) Debug.Log(message); else Debug.LogError(message);
            return condition;
        }

        private void Capture(string name)
        {
            var camera = view != null ? view : Camera.main;
            if (camera == null) return;
            int width = Mathf.Max(1, Screen.width), height = Mathf.Max(1, Screen.height);
            var texture = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            var previousTarget = camera.targetTexture;
            var previousActive = RenderTexture.active;
            Texture2D pixels = null;
            try
            {
                camera.targetTexture = texture;
                Canvas.ForceUpdateCanvases();
                if (visor != null && game != null) visor.RefreshAim(game);
                camera.Render();
                RenderTexture.active = texture;
                pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
                pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                pixels.Apply();
                File.WriteAllBytes(Path.Combine(output, name), pixels.EncodeToPNG());
                results.Add($"SCREENSHOT {name} {width}x{height}");
            }
            finally
            {
                camera.targetTexture = previousTarget;
                RenderTexture.active = previousActive;
                if (pixels != null) Destroy(pixels);
                texture.Release();
                Destroy(texture);
            }
        }
    }
}
