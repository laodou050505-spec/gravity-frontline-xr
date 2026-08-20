using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ByteDance.PICO.XR;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEditor.XR.Management;
using UnityEditor.XR.Management.Metadata;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Management;

namespace GravityRepulsion.Editor
{
    public static class GravityExistingSceneUpgrader
    {
        public const string ScenePath = "Assets/GravityWarden/Scenes/GravityBoulevard.unity";
        public const string TitleScenePath = "Assets/GravityWarden/Scenes/GravityTitle.unity";
        private const string BackupPath = "Assets/GravityWarden/Scenes/GravityBoulevard_UserLayoutBackup.unity";
        private const string PrefabRoot = "Assets/GravityWarden/Prefabs";
        private const string MaterialRoot = "Assets/GravityWarden/Materials";
        private const string AssetRoot = "Assets/机械近代欧式街道";
        private const string LoaderType = "ByteDance.PICO.XR.PXR_Loader";

        private static readonly Color Violet = new(.52f, .18f, 1f);
        private static readonly Color Cyan = new(.12f, .7f, 1f);
        private static readonly Color Warm = new(1f, .55f, .22f);

        private sealed class GameplayPrefabs
        {
            public GameObject missile26;
            public GameObject missile28;
            public GameObject health27;
            public GameObject robot29;
            public GameObject robot30;
            public GameObject robot31;
        }

        [MenuItem("引力 斥力/升级当前摆位玩法（非破坏）")]
        public static void UpgradeExistingScene()
        {
            EnsureFolders();
            BackupUserLayout();
            ConfigureProjectAndPico();
            var prefabs = BuildGameplayPrefabs();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            UpgradeScene(scene, prefabs);
            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            Debug.Log("GRAVITY_EXISTING_SCENE_UPGRADE_PASS preservedLayout=true models=26,27,28,29,30,31");
        }

        public static void BatchUpgrade() => UpgradeExistingScene();

        [MenuItem("引力 斥力/验证当前摆位与 PICO 配置（非破坏）")]
        public static void ValidateExistingScene()
        {
            var errors = new List<string>();
            ConfigureProjectAndPico();
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var manager = UnityEngine.Object.FindFirstObjectByType<GravityGameManager>();
            if (manager == null) errors.Add("GravityGameManager missing");
            var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
            var designStart = UnityEngine.Object.FindFirstObjectByType<GravityDesignStart>();
            if (designStart == null) errors.Add("Single GravityDesignStart is required for desktop/XR/recenter alignment");
            else if (origin == null || !designStart.MatchesRig()) errors.Add("XR Origin does not match the authoritative design player start");
            var xrCamera = Camera.main;
            if (xrCamera == null || designStart == null || Mathf.Abs(xrCamera.transform.localPosition.y - designStart.eyeHeight) > .02f
                                 || Mathf.Abs(Mathf.DeltaAngle(xrCamera.transform.localEulerAngles.y, 0f)) > .1f)
                errors.Add("XR head camera local eye height/yaw does not derive from design start");
            if (UnityEngine.Object.FindObjectsByType<GravityHand>(FindObjectsSortMode.None).Length != 2) errors.Add("Two GravityHand components are required");
            if (manager != null)
            {
                if (manager.scoutRobotPrefab == null || manager.engineerRobotPrefab == null || manager.guardRobotPrefab == null) errors.Add("Robot prefabs are not assigned");
                if (manager.healthPackPrefab == null) errors.Add("Health pack prefab is not assigned");
                ValidateController(manager.leftHand, "left", errors);
                ValidateController(manager.rightHand, "right", errors);
                var hudCanvas = manager.hud != null ? manager.hud.GetComponent<Canvas>() : null;
                if (hudCanvas == null || hudCanvas.renderMode != RenderMode.WorldSpace || manager.hud.transform.parent != null || !manager.hud.WorldSpaceConfigured)
                    errors.Add("Mission board must be a detached world-space canvas, never a camera HUD");
                if (designStart != null && manager.hud != null)
                {
                    var missionBoardDistance = Vector3.Distance(manager.hud.transform.position, designStart.MissionBoardPosition);
                    Debug.Log($"GRAVITY_WORLD_BOARD_LAYOUT designStart={designStart.worldPosition}/{designStart.worldYaw:0.0}/{designStart.eyeHeight:0.000} board={manager.hud.transform.position} expected={designStart.MissionBoardPosition} distance={missionBoardDistance:0.0000} parent={(manager.hud.transform.parent == null ? "null" : HierarchyPath(manager.hud.transform.parent))}");
                    if (missionBoardDistance > .03f)
                        errors.Add("Mission board is not anchored from design player start");
                }
            }
            var preview = GameObject.Find("SCENE_PREVIEW_ENEMIES_仅Scene查看");
            if (preview == null) errors.Add("Scene preview root missing");
            else
            {
                var previewGameplay = preview.GetComponentsInChildren<MonoBehaviour>(true)
                    .Where(b => b is GravityTarget || b is RobotEnemy || b is RobotBreakApartEffect).ToArray();
                if (previewGameplay.Length > 0)
                    errors.Add("Scene preview contains gameplay behaviours: " + string.Join(", ", previewGameplay.Select(b => b.GetType().Name + "@" + HierarchyPath(b.transform))));
            }
            ValidatePrefab(PrefabRoot + "/EnemyProjectile_26.prefab", GravityTargetKind.Projectile, errors, 26);
            ValidatePrefab(PrefabRoot + "/EnemyProjectile_28.prefab", GravityTargetKind.Projectile, errors, 28);
            ValidatePrefab(PrefabRoot + "/HealthPack.prefab", GravityTargetKind.Heal, errors, 27);
            ValidateProjectileFacing(PrefabRoot + "/EnemyProjectile_26.prefab", errors, 26);
            ValidateProjectileFacing(PrefabRoot + "/EnemyProjectile_28.prefab", errors, 28);
            ValidateHealthFacing(PrefabRoot + "/HealthPack.prefab", errors);
            if (manager != null && (manager.ProjectileConcurrencyLimit != 2 || manager.EnemyShotInterval < 1.35f || manager.EnemyShotInterval > 1.45f))
                errors.Add($"Enemy projectile pacing is outside the intended wave-1 range: concurrent={manager.ProjectileConcurrencyLimit} interval={manager.EnemyShotInterval:0.000}");
            ValidateRobot(PrefabRoot + "/Robot_Scout.prefab", errors, 29);
            ValidateRobot(PrefabRoot + "/Robot_Engineer.prefab", errors, 30);
            ValidateRobot(PrefabRoot + "/Robot_Guard.prefab", errors, 31);
            if (!File.Exists(BackupPath)) errors.Add("User layout backup scene missing");
            if (UnityEngine.Object.FindFirstObjectByType<GravityNightSky>() == null) errors.Add("Original procedural moon/star night sky missing");
            var buildScenes = EditorBuildSettings.scenes;
            if (buildScenes.Length < 2 || !buildScenes[0].enabled || buildScenes[0].path != TitleScenePath)
                errors.Add("Title scene must be the isolated first build scene");
            if (buildScenes.All(s => !s.enabled || s.path != ScenePath)) errors.Add("Gameplay scene missing from Build Settings");
            ValidateIsolatedTitleScene(errors);
            // The isolated-title check opens its own scene, so explicitly restore the authored
            // gameplay scene before continuing its object-level validations.
            scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            manager = UnityEngine.Object.FindFirstObjectByType<GravityGameManager>();
            designStart = UnityEngine.Object.FindFirstObjectByType<GravityDesignStart>();
            var sceneTransforms = scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true)).ToArray();
            if (sceneTransforms.Any(item => item != null && (item.name.StartsWith("Prop_Crate_", StringComparison.Ordinal)
                                                           || item.name.StartsWith("Prop_Bin_", StringComparison.Ordinal)
                                                           || item.name.StartsWith("Prop_EnergyCanister_", StringComparison.Ordinal))))
                errors.Add("Synthetic crate/bin/canister placeholders remain in scene");
            if (sceneTransforms.Any(item => item != null && item.name.StartsWith("VISUAL_可吸道具_", StringComparison.Ordinal)))
                errors.Add("Generated pullable visual remains in scene");
            var pullableSceneModels = UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(target => target.kind == GravityTargetKind.Prop).ToArray();
            if (pullableSceneModels.Length < 3) errors.Add("At least three confirmed existing scene models must be pullable");
            if (pullableSceneModels.Select(target => target.displayName).Distinct().Count() < 3) errors.Add("Pullable scene-model whitelist has fewer than three confirmed identities");
            foreach (var prop in pullableSceneModels)
            {
                if (!prop.MatchesApprovedPropIdentity()) errors.Add($"Unapproved or misnamed scene pullable {HierarchyPath(prop.transform)}/{prop.displayName}");
                if (!prop.HasValidVisibleInteractionGeometry(out var propFailure)) errors.Add($"Invalid existing-scene pullable {HierarchyPath(prop.transform)}: {propFailure}");
                var body = prop.GetComponent<Rigidbody>();
                if (body == null || !body.isKinematic || body.useGravity || body.linearVelocity.sqrMagnitude > .0001f || body.angularVelocity.sqrMagnitude > .0001f)
                    errors.Add($"Scene pullable is not initially static and stable: {HierarchyPath(prop.transform)}");
                if (prop.GetComponentsInChildren<MeshCollider>(true).Any(collider => collider.enabled && !collider.isTrigger && !prop.IsSafeTriggerCollider(collider) && !collider.convex))
                    errors.Add($"Scene pullable has a non-convex dynamic MeshCollider: {HierarchyPath(prop.transform)}");
            }
            if (pullableSceneModels.Any(prop => HasUnsafeInitialOverlap(prop, pullableSceneModels)))
                errors.Add("A pullable scene model still has a dangerous initial collider penetration");
            if (designStart != null && pullableSceneModels.Count(prop => Vector3.Distance(prop.transform.position, designStart.worldPosition) > 1.5f) < 3)
                errors.Add("World-space gameplay props are not distributed around the battle street");
            foreach (var requiredIdentity in GravityTarget.ApprovedScenePropDisplayNames)
                if (!pullableSceneModels.Any(prop => prop.displayName == requiredIdentity && prop.MatchesApprovedPropIdentity()))
                    errors.Add($"Required existing scene model is not pullable: {requiredIdentity}");
            Debug.Log($"GRAVITY_PROP_IDENTITY_VALIDATION identities={string.Join(",", pullableSceneModels.Select(prop => prop.displayName).Distinct().OrderBy(value => value))} initialBodiesStatic=true generatedModels=false");

            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            var loader = xr?.AssignedSettings?.activeLoaders.Any(l => l != null && l.GetType().FullName == LoaderType) == true;
            if (!loader) errors.Add("Official PXR_Loader not assigned");
            if (PlayerSettings.Android.targetArchitectures != AndroidArchitecture.ARM64) errors.Add("Android is not ARM64-only");
            if (PlayerSettings.GetScriptingBackend(NamedBuildTarget.Android) != ScriptingImplementation.IL2CPP) errors.Add("Android is not IL2CPP");
            if (PlayerSettings.Android.applicationEntry != AndroidApplicationEntry.Activity) errors.Add("Android entry is not Activity");
            var pxr = PXR_Settings.GetSettings();
            if (pxr == null || pxr.stereoRenderingModeAndroid != PXR_Settings.StereoRenderingModeAndroid.Multiview) errors.Add("PICO Multiview not enabled");
            if (ReadInputHandler() != 1) errors.Add("Input System is not the sole input backend");
            if (errors.Count > 0) throw new BuildFailedException("GRAVITY_VALIDATION_FAIL\n- " + string.Join("\n- ", errors));
            Debug.Log($"GRAVITY_EXISTING_VALIDATION_PASS scene={scene.path} layoutBackup=true realModels=true pullables={UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None).Count(t => t.kind == GravityTargetKind.Prop)} worldSpaceMissionBoard=true designStart=({designStart?.worldPosition})/{designStart?.worldYaw:0.0}/{designStart?.eyeHeight:0.000} picoLoader=true multiview=true arm64=true il2cpp=true activity=true inputSystemOnly=true");
        }

        public static void BatchValidate() => ValidateExistingScene();

        [MenuItem("引力 斥力/生成独立启动封面场景")]
        public static void CreateOrUpdateTitleScene()
        {
            EnsureFolders();
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GravityTitle";

            var titleRoot = new GameObject("TITLE_ROOT_独立启动阶段");
            var designStart = titleRoot.AddComponent<GravityDesignStart>();
            designStart.worldPosition = Vector3.zero;
            designStart.worldYaw = 0f;
            designStart.eyeHeight = 1.72f;
            designStart.gameplayLandmark = titleRoot.transform;
            designStart.missionBoardOffset = Vector3.zero;
            designStart.ApplyToRig();
            var origin = titleRoot.AddComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0f;
            titleRoot.AddComponent<PXR_Manager>();

            var cameraRoot = new GameObject("TITLE_CAMERA_RIG_权威起点");
            cameraRoot.transform.SetParent(titleRoot.transform, false);
            origin.CameraFloorOffsetObject = cameraRoot;
            var camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.transform.SetParent(cameraRoot.transform, false);
            camera.transform.localPosition = Vector3.up * designStart.eyeHeight;
            camera.transform.localRotation = Quaternion.identity;
            var titleCamera = camera.AddComponent<Camera>();
            titleCamera.clearFlags = CameraClearFlags.SolidColor;
            titleCamera.backgroundColor = new Color(.006f, .009f, .024f);
            titleCamera.nearClipPlane = .05f;
            titleCamera.farClipPlane = 80f;
            titleCamera.fieldOfView = 67f;
            camera.AddComponent<AudioListener>();
            origin.Camera = titleCamera;

            var canvasGo = new GameObject("TITLE_CANVAS_不透明封面", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(titleRoot.transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = titleCamera;
            canvas.planeDistance = .4f;
            canvas.overrideSorting = true;
            canvas.sortingOrder = 1000;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1600f, 1000f);
            scaler.matchWidthOrHeight = .5f;

            var cover = new GameObject("OPAQUE_TITLE_COVER", typeof(RectTransform), typeof(Image));
            cover.transform.SetParent(canvasGo.transform, false);
            var coverRect = cover.GetComponent<RectTransform>();
            coverRect.anchorMin = Vector2.zero;
            coverRect.anchorMax = Vector2.one;
            coverRect.offsetMin = coverRect.offsetMax = Vector2.zero;
            cover.GetComponent<Image>().color = new Color(.006f, .009f, .024f, 1f);

            TitleText("TITLE_引力斥力", canvasGo.transform, new Vector2(0f, 170f), new Vector2(1200f, 170f), "引 力  ·  斥 力", 104, new Color(.84f, .72f, 1f), TextAnchor.MiddleCenter);
            TitleText("SUBTITLE", canvasGo.transform, new Vector2(0f, 56f), new Vector2(1050f, 72f), "重力战线 · 街道防卫 XR", 34, new Color(.36f, .84f, 1f), TextAnchor.MiddleCenter);
            TitleText("RULE", canvasGo.transform, new Vector2(0f, -52f), new Vector2(980f, 112f), "抬手锁定 · 双极对撞 · 斥力反击", 28, new Color(.82f, .87f, 1f), TextAnchor.MiddleCenter);
            var start = TitleText("START", canvasGo.transform, new Vector2(0f, -190f), new Vector2(500f, 92f), "START  开始战斗", 36, new Color(1f, .9f, .54f), TextAnchor.MiddleCenter);
            start.GetComponent<Text>().fontStyle = FontStyle.Bold;
            TitleText("EXIT", canvasGo.transform, new Vector2(0f, -268f), new Vector2(500f, 52f), "ESC / EXIT  退出", 20, new Color(.59f, .66f, .8f), TextAnchor.MiddleCenter);
            TitleText("TITLE_HELP", canvasGo.transform, new Vector2(0f, -367f), new Vector2(900f, 54f), "Enter / Space / 扳机：开始    C / 左 Y：重新居中", 19, new Color(.47f, .55f, .73f), TextAnchor.MiddleCenter);

            titleRoot.AddComponent<GravityTitleController>().designStart = designStart;
            EditorSceneManager.SaveScene(scene, TitleScenePath);
            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(TitleScenePath, true),
                new EditorBuildSettingsScene(ScenePath, true)
            };
            AssetDatabase.SaveAssets();
            Debug.Log("GRAVITY_TITLE_SCENE_BUILD_PASS gameplayLoaded=false opaqueCover=true buildIndex=0");
        }

        public static void BatchCreateTitleScene() => CreateOrUpdateTitleScene();

        [MenuItem("引力 斥力/构建当前摆位 macOS 测试版")]
        public static void BuildMac()
        {
            ValidateExistingScene();
            Directory.CreateDirectory("Builds/macOS");
            Build(BuildTarget.StandaloneOSX, "Builds/macOS/引力 斥力.app", BuildOptions.Development);
        }

        public static void BatchBuildMac() => BuildMac();

        [MenuItem("引力 斥力/构建当前摆位 PICO APK")]
        public static void BuildAndroid()
        {
            ValidateExistingScene();
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
            Directory.CreateDirectory("Builds/PICO");
            Build(BuildTarget.Android, "Builds/PICO/引力 斥力-PICO.apk", BuildOptions.None);
        }

        public static void BatchBuildAndroid() => BuildAndroid();

        [MenuItem("引力 斥力/捕获实际 Game 视图")]
        public static void CaptureGameView()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var camera = Camera.main ?? throw new InvalidOperationException("Main Camera missing");
            var manager = UnityEngine.Object.FindFirstObjectByType<GravityGameManager>();
            var originalPosition = camera.transform.position;
            var originalRotation = camera.transform.rotation;
            var originalTarget = camera.targetTexture;
            var staged = new List<GameObject>();
            var preview = GameObject.Find("SCENE_PREVIEW_ENEMIES_仅Scene查看");
            var previewWasActive = preview != null && preview.activeSelf;
            try
            {
                if (preview != null) preview.SetActive(false);
                var designStart = UnityEngine.Object.FindFirstObjectByType<GravityDesignStart>();
                camera.transform.position = designStart != null ? designStart.WorldEyePosition : new Vector3(0f, 2.05f, 10.5f);
                camera.transform.rotation = designStart != null ? designStart.WorldRotation : Quaternion.Euler(0f, 180f, 0f);
                var nightSky = UnityEngine.Object.FindFirstObjectByType<GravityNightSky>();
                if (nightSky != null) nightSky.RefreshNow(camera.transform);
                if (manager != null)
                {
                    staged.Add(StageForCapture(manager.scoutRobotPrefab, new Vector3(-3.8f, 0f, -2.4f), camera.transform.position));
                    staged.Add(StageForCapture(manager.guardRobotPrefab, new Vector3(0f, 0f, -4.8f), camera.transform.position));
                    staged.Add(StageForCapture(manager.engineerRobotPrefab, new Vector3(3.8f, 0f, -2.4f), camera.transform.position));
                    if (manager.healthPackPrefab != null) staged.Add(StageForCapture(manager.healthPackPrefab, new Vector3(-5.8f, 1.15f, 1.2f), camera.transform.position));
                }
                var renderTarget = new RenderTexture(1600, 1000, 24, RenderTextureFormat.ARGB32);
                camera.targetTexture = renderTarget;
                camera.Render();
                RenderTexture.active = renderTarget;
                var capture = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
                capture.ReadPixels(new Rect(0, 0, renderTarget.width, renderTarget.height), 0, 0);
                capture.Apply();
                Directory.CreateDirectory("Preview");
                File.WriteAllBytes("Preview/GravityBoulevard_ActualGameView.png", capture.EncodeToPNG());
                camera.targetTexture = originalTarget;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(renderTarget);
                UnityEngine.Object.DestroyImmediate(capture);
                Debug.Log("GRAVITY_GAME_VIEW_CAPTURE_PASS Preview/GravityBoulevard_ActualGameView.png stagedRealModels=true");
            }
            finally
            {
                foreach (var item in staged) if (item != null) UnityEngine.Object.DestroyImmediate(item);
                camera.targetTexture = originalTarget;
                camera.transform.position = originalPosition;
                camera.transform.rotation = originalRotation;
                if (preview != null) preview.SetActive(previewWasActive);
            }
        }

        [MenuItem("引力 斥力/生成 26-31 多角度校准图")]
        public static void CaptureModelOrientationSheet()
        {
            var previous = SceneManager.GetActiveScene().path;
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.46f, .5f, .62f);
            var light = new GameObject("Key Light").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.35f;
            light.transform.rotation = Quaternion.Euler(42f, -36f, 0f);
            var camera = new GameObject("Orientation Camera").AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 6.8f;
            camera.transform.position = new Vector3(0f, 5.8f, -16f);
            camera.transform.LookAt(new Vector3(0f, 3.8f, 0f));
            camera.backgroundColor = new Color(.018f, .025f, .055f);
            camera.clearFlags = CameraClearFlags.SolidColor;

            var ids = new[] { 26, 27, 28, 29, 30, 31 };
            for (var row = 0; row < ids.Length; row++)
            for (var col = 0; col < 4; col++)
            {
                var model = LoadNumberedModel(ids[row]);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
                instance.name = $"{ids[row]}_{col * 90}deg";
                instance.transform.position = new Vector3((col - 1.5f) * 3.1f, (ids.Length - 1 - row) * 2.0f, 0f);
                instance.transform.rotation = Quaternion.Euler(0f, col * 90f, 0f);
                NormalizeModel(instance.transform, ids[row] >= 29 ? 1.65f : 1.15f, false);
                CreateLabel(instance.name, instance.transform.position + Vector3.up * 1.05f);
            }
            camera.orthographicSize = 6.5f;
            camera.transform.position = new Vector3(0f, 5.1f, -17f);
            camera.transform.rotation = Quaternion.identity;
            var texture = new RenderTexture(2200, 1500, 24, RenderTextureFormat.ARGB32);
            camera.targetTexture = texture;
            camera.Render();
            RenderTexture.active = texture;
            var image = new Texture2D(texture.width, texture.height, TextureFormat.RGB24, false);
            image.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
            image.Apply();
            Directory.CreateDirectory("Preview");
            File.WriteAllBytes("Preview/Models_26-31_OrientationSheet.png", image.EncodeToPNG());
            camera.targetTexture = null;
            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(texture);
            UnityEngine.Object.DestroyImmediate(image);
            if (!string.IsNullOrEmpty(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            Debug.Log("GRAVITY_ORIENTATION_SHEET_PASS Preview/Models_26-31_OrientationSheet.png");
        }

        public static void BatchCaptureOrientation() => CaptureModelOrientationSheet();
        public static void BatchCaptureGameView() => CaptureGameView();

        public static void BatchAuditExistingBenchModels()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var bench in scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                         .Where(item => item != null && item.name.StartsWith("Prop_Bench_", StringComparison.Ordinal)))
            {
                var candidates = UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                    .Where(renderer => renderer != null && renderer.enabled && !renderer.transform.IsChildOf(bench))
                    .Select(renderer => new { renderer, distance = Vector3.Distance(renderer.bounds.center, bench.position) })
                    .Where(item => item.distance < 4f)
                    .OrderBy(item => item.distance)
                    .Take(10)
                    .ToArray();
                Debug.Log($"BENCH_MODEL_AUDIT bench={HierarchyPath(bench)} position={bench.position} candidates=" +
                          string.Join(" | ", candidates.Select(item =>
                          {
                              var source = PrefabUtility.GetCorrespondingObjectFromSource(item.renderer.gameObject);
                              var sourcePath = source != null ? AssetDatabase.GetAssetPath(source) : "scene-native";
                              return $"{HierarchyPath(item.renderer.transform)} d={item.distance:0.00} bounds={item.renderer.bounds.size} source={sourcePath}";
                          })));
            }
        }

        public static void BatchCaptureExistingInteractiveModels()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var interactiveRoot = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .FirstOrDefault(item => item != null && item.name == "INTERACTIVE_用户摆位可吸取物_自动标记");
            if (interactiveRoot == null) throw new BuildFailedException("Interactive scene-model root is missing");

            var candidates = Enumerable.Range(0, interactiveRoot.childCount)
                .Select(interactiveRoot.GetChild)
                .Where(item => item.GetComponentsInChildren<Renderer>(true)
                    .Any(renderer => renderer != null && renderer.bounds.extents.sqrMagnitude > .0004f))
                .ToArray();
            if (candidates.Length == 0) throw new BuildFailedException("Interactive scene-model root has no visible child models");

            const string outputFolder = "Preview/InteractiveModelAudit";
            Directory.CreateDirectory(outputFolder);
            foreach (var old in Directory.GetFiles(outputFolder, "*.png")) File.Delete(old);

            var originalActive = scene.GetRootGameObjects().ToDictionary(item => item, item => item.activeSelf);
            var manifest = new List<string>();
            foreach (var root in scene.GetRootGameObjects()) root.SetActive(false);

            var cameraObject = new GameObject("Interactive Model Audit Camera");
            var camera = cameraObject.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(.035f, .045f, .06f);
            camera.fieldOfView = 34f;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 100f;
            camera.transform.position = new Vector3(4.8f, 3.2f, 5.6f);

            var keyLight = new GameObject("Interactive Model Audit Key").AddComponent<Light>();
            keyLight.type = LightType.Directional;
            keyLight.intensity = 1.35f;
            keyLight.color = new Color(1f, .82f, .66f);
            keyLight.transform.rotation = Quaternion.Euler(38f, -42f, 0f);
            var fillLight = new GameObject("Interactive Model Audit Fill").AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = .85f;
            fillLight.color = new Color(.42f, .65f, 1f);
            fillLight.transform.rotation = Quaternion.Euler(26f, 138f, 0f);

            var renderTarget = new RenderTexture(1024, 1024, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(renderTarget.width, renderTarget.height, TextureFormat.RGB24, false);
            camera.targetTexture = renderTarget;
            try
            {
                for (var index = 0; index < candidates.Length; index++)
                {
                    var source = candidates[index];
                    var clone = UnityEngine.Object.Instantiate(source.gameObject);
                    clone.name = source.name;
                    clone.SetActive(true);
                    foreach (var behaviour in clone.GetComponentsInChildren<MonoBehaviour>(true)) behaviour.enabled = false;
                    foreach (var body in clone.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;

                    clone.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0f, 24f, 0f));
                    var bounds = CalculateBounds(clone);
                    var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
                    clone.transform.localScale *= 3.2f / Mathf.Max(.001f, largest);
                    bounds = CalculateBounds(clone);
                    clone.transform.position += new Vector3(-bounds.center.x, -bounds.min.y, -bounds.center.z);
                    bounds = CalculateBounds(clone);

                    var target = new Vector3(0f, Mathf.Max(.4f, bounds.center.y), 0f);
                    camera.transform.LookAt(target);
                    camera.Render();
                    RenderTexture.active = renderTarget;
                    image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0);
                    image.Apply();

                    var meshAssetPaths = clone.GetComponentsInChildren<Renderer>(true)
                        .Select(renderer => renderer switch
                        {
                            SkinnedMeshRenderer skinned => AssetDatabase.GetAssetPath(skinned.sharedMesh),
                            MeshRenderer meshRenderer => AssetDatabase.GetAssetPath(meshRenderer.GetComponent<MeshFilter>()?.sharedMesh),
                            _ => string.Empty
                        })
                        .Where(path => !string.IsNullOrEmpty(path))
                        .Distinct()
                        .ToArray();
                    var sourcePath = meshAssetPaths.FirstOrDefault() ?? "scene-native";
                    var assetNumber = sourcePath.StartsWith(AssetRoot + "/", StringComparison.Ordinal)
                        ? ParseAssetNumber(sourcePath)
                        : -1;
                    var safeName = string.Concat(source.name.Select(character => Path.GetInvalidFileNameChars().Contains(character) ? '_' : character));
                    var filename = $"{index + 1:00}_asset{assetNumber:00}_{safeName}.png";
                    File.WriteAllBytes(Path.Combine(outputFolder, filename), image.EncodeToPNG());

                    var gravityTarget = source.GetComponent<GravityTarget>();
                    manifest.Add($"index={index + 1:00} asset={assetNumber} name={source.name} hierarchy={HierarchyPath(source)} bounds={bounds.size} renderers={source.GetComponentsInChildren<Renderer>(true).Length} colliders={source.GetComponentsInChildren<Collider>(true).Length} gravityTarget={(gravityTarget != null ? gravityTarget.displayName : "none")} source={sourcePath}");
                    UnityEngine.Object.DestroyImmediate(clone);
                }
            }
            finally
            {
                camera.targetTexture = null;
                RenderTexture.active = null;
                UnityEngine.Object.DestroyImmediate(renderTarget);
                UnityEngine.Object.DestroyImmediate(image);
                UnityEngine.Object.DestroyImmediate(cameraObject);
                UnityEngine.Object.DestroyImmediate(keyLight.gameObject);
                UnityEngine.Object.DestroyImmediate(fillLight.gameObject);
                foreach (var state in originalActive) if (state.Key != null) state.Key.SetActive(state.Value);
            }

            File.WriteAllLines(Path.Combine(outputFolder, "manifest.txt"), manifest);
            Debug.Log($"GRAVITY_INTERACTIVE_MODEL_AUDIT_PASS models={candidates.Length} output={outputFolder}");
        }

        [MenuItem("引力 斥力/审计 29-31 真实拆分层级")]
        public static void AuditRobotHierarchy()
        {
            var lines = new List<string>();
            foreach (var number in new[] { 29, 30, 31 })
            {
                var source = LoadNumberedModel(number);
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
                var sourceStats = GeometryStats(instance);
                UnityEngine.Object.DestroyImmediate(instance);
                var prefabPath = number == 29 ? PrefabRoot + "/Robot_Scout.prefab" : number == 30 ? PrefabRoot + "/Robot_Engineer.prefab" : PrefabRoot + "/Robot_Guard.prefab";
                var contents = PrefabUtility.LoadPrefabContents(prefabPath);
                var prefabStats = GeometryStats(contents);
                PrefabUtility.UnloadPrefabContents(contents);
                var line = $"ROBOT_HIERARCHY asset={number} source[{sourceStats}] prefab[{prefabStats}] dependency={AssetDatabase.GetDependencies(prefabPath, true).Contains(AssetDatabase.GetAssetPath(source))}";
                lines.Add(line);
                Debug.Log(line);
            }
            Directory.CreateDirectory("Logs");
            File.WriteAllLines("Logs/RobotHierarchyAudit.log", lines);
        }

        public static void BatchAuditRobotHierarchy() => AuditRobotHierarchy();

        public static void BatchAuditPullableColliders()
        {
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            foreach (var target in UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(item => item.kind == GravityTargetKind.Prop))
                Debug.Log($"PULLABLE_COLLIDER_AUDIT path={HierarchyPath(target.transform)} identity={target.displayName} colliders=" +
                          string.Join("|", target.GetComponentsInChildren<Collider>(true).Select(collider =>
                              collider is MeshCollider mesh
                                  ? $"MeshCollider(enabled={mesh.enabled},trigger={mesh.isTrigger},convex={mesh.convex})"
                                  : $"{collider.GetType().Name}(enabled={collider.enabled},trigger={collider.isTrigger})")));
        }

        private static GameplayPrefabs BuildGameplayPrefabs()
        {
            var missile26 = CreateProjectilePrefab(26, "EnemyProjectile_26.prefab", new Color(1f, .18f, .12f), 0f);
            var missile28 = CreateProjectilePrefab(28, "EnemyProjectile_28.prefab", new Color(1f, .48f, .1f), 0f);
            var health = CreateHealthPrefab();
            var scout = CreateRobotPrefab(29, "Robot_Scout.prefab", "巡逻机 29", 1, 16, 1.08f, 2.65f, missile26, missile28, 180f, 1);
            var engineer = CreateRobotPrefab(30, "Robot_Engineer.prefab", "工程机 30", 1, 18, .92f, 2.35f, missile28, missile26, 180f, 1);
            var guard = CreateRobotPrefab(31, "Robot_Guard.prefab", "盾卫核心 31", 2, 24, .72f, 1.9f, missile26, missile28, 180f, 2);
            return new GameplayPrefabs { missile26 = missile26, missile28 = missile28, health27 = health, robot29 = scout, robot30 = engineer, robot31 = guard };
        }

        private static GameObject CreateProjectilePrefab(int number, string filename, Color trailColor, float yaw)
        {
            var root = new GameObject("EnemyProjectile_" + number + "_危险导弹");
            var collider = root.AddComponent<SphereCollider>();
            collider.radius = .3f;
            collider.isTrigger = true;
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.mass = .35f;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var target = root.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Projectile;
            target.displayName = "危险导弹 " + number;
            target.canBlockOnce = false;
            var visualRoot = new GameObject("VISUAL_真实导弹模型_" + number).transform;
            visualRoot.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadNumberedModel(number));
            model.transform.SetParent(visualRoot, false);
            model.transform.localRotation = Quaternion.identity;
            NormalizeModel(model.transform, .7f, true);
            visualRoot.localRotation = Quaternion.Euler(0f, yaw, 0f);
            target.replaceableVisual = visualRoot;
            var projectile = root.AddComponent<EnemyProjectile>();
            projectile.visualForward = visualRoot;
            projectile.modelForwardAxis = Vector3.up;
            projectile.modelRotationOffset = new Vector3(0f, yaw, 0f);
            var trail = root.AddComponent<TrailRenderer>();
            trail.time = .38f;
            trail.startWidth = .14f;
            trail.endWidth = 0f;
            trail.material = RuntimeMaterial("M_MissileTrail_" + number, trailColor, trailColor * 4f);
            trail.startColor = new Color(trailColor.r, trailColor.g, trailColor.b, .9f);
            trail.endColor = new Color(trailColor.r, trailColor.g, trailColor.b, 0f);
            var glow = root.AddComponent<Light>();
            glow.type = LightType.Point;
            glow.color = trailColor;
            glow.intensity = 1.65f;
            glow.range = 2.8f;
            glow.renderMode = LightRenderMode.ForceVertex;
            target.RefreshGeometryCache();
            return SavePrefab(root, PrefabRoot + "/" + filename);
        }

        private static GameObject CreateHealthPrefab()
        {
            var root = new GameObject("HealthPack_27_防卫补给");
            var collider = root.AddComponent<BoxCollider>();
            collider.size = new Vector3(.72f, .68f, .42f);
            collider.isTrigger = true;
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            body.isKinematic = true;
            var target = root.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Heal;
            target.displayName = "防卫补给 27";
            target.canBlockOnce = false;
            var visualRoot = new GameObject("VISUAL_真实血包模型_27").transform;
            visualRoot.SetParent(root.transform, false);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadNumberedModel(27));
            model.transform.SetParent(visualRoot, false);
            model.transform.localRotation = Quaternion.identity;
            NormalizeModel(model.transform, .72f, true);
            visualRoot.localRotation = Quaternion.Euler(0f, 90f, 0f);
            target.replaceableVisual = visualRoot;
            var billboard = root.AddComponent<FacePlayerBillboard>();
            billboard.visualRoot = visualRoot;
            billboard.yawOffset = 90f;
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.2f, 1f, .62f);
            light.intensity = 1.5f;
            light.range = 3.4f;
            var particleRoot = new GameObject("VFX_绿色回血粒子_27");
            particleRoot.transform.SetParent(root.transform, false);
            var particleSystem = particleRoot.AddComponent<ParticleSystem>();
            var main = particleSystem.main;
            main.loop = true;
            main.duration = 1.4f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(.7f, 1.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .28f);
            main.startSize = new ParticleSystem.MinMaxCurve(.035f, .085f);
            main.startColor = new Color(.18f, 1f, .48f, .92f);
            main.maxParticles = 34;
            main.gravityModifier = -.06f;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            var emission = particleSystem.emission;
            emission.rateOverTime = 16f;
            var shape = particleSystem.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = .28f;
            var colorOverLifetime = particleSystem.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(new Color(.35f, 1f, .62f), 0f), new GradientColorKey(new Color(.08f, .85f, .3f), 1f) },
                new[] { new GradientAlphaKey(.05f, 0f), new GradientAlphaKey(.9f, .16f), new GradientAlphaKey(.65f, .72f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);
            var particleRenderer = particleRoot.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.material = HealthParticleMaterial();
            target.RefreshGeometryCache();
            return SavePrefab(root, PrefabRoot + "/HealthPack.prefab");
        }

        private static GameObject CreateRobotPrefab(int number, string filename, string display, int health, int charge, float speed, float interval, GameObject projectile, GameObject alternate, float visualYaw, int pullStages)
        {
            var root = new GameObject("Robot_" + number + "_" + display);
            var collider = root.AddComponent<CapsuleCollider>();
            collider.height = number == 31 ? 2.55f : 2.3f;
            collider.radius = number == 31 ? .72f : .62f;
            collider.center = Vector3.up * collider.height * .5f;
            var body = root.AddComponent<Rigidbody>();
            body.mass = number == 31 ? 4.4f : 2.7f;
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            var target = root.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Robot;
            target.displayName = display;
            target.maxHealth = health;
            target.ultimateCharge = charge;
            target.canBlockOnce = true;
            target.pullStagesRequired = pullStages;
            target.heldFollowSharpness = number == 31 ? 8f : 10f;
            target.heldStruggle = number == 31 ? .022f : .034f;
            var visualCalibration = new GameObject("VISUAL_CALIBRATION_模型坐标轴校准_" + number).transform;
            visualCalibration.SetParent(root.transform, false);
            visualCalibration.localRotation = Quaternion.Euler(0f, 90f, 0f);
            var model = (GameObject)PrefabUtility.InstantiatePrefab(LoadNumberedModel(number));
            model.transform.SetParent(visualCalibration, false);
            model.transform.localRotation = Quaternion.Euler(0f, visualYaw, 0f);
            NormalizeModel(model.transform, number == 31 ? 2.42f : 2.18f, true);
            target.replaceableVisual = visualCalibration;
            var muzzle = new GameObject("ProjectileMuzzle_导弹发射点").transform;
            muzzle.SetParent(root.transform, false);
            muzzle.localPosition = new Vector3(0f, 1.45f, .68f);
            var enemy = root.AddComponent<RobotEnemy>();
            enemy.moveSpeed = speed;
            enemy.attackInterval = interval;
            enemy.projectilePrefab = projectile;
            enemy.alternateProjectilePrefab = alternate;
            enemy.muzzle = muzzle;
            enemy.visualRoot = model.transform;
            enemy.visualFacingProbe = model.transform;
            enemy.visualYawOffset = visualYaw;
            var rigMotion = root.AddComponent<RobotRigMotion>();
            rigMotion.rigRoot = model.transform;
            rigMotion.legSwingDegrees = number == 31 ? 20f : 24f;
            rigMotion.armSwingDegrees = number == 31 ? 14f : 17f;
            rigMotion.heldSwayPitchDegrees = number == 31 ? 10f : 12f;
            rigMotion.heldSwayRollDegrees = number == 31 ? 12f : 15f;
            var breakApart = root.AddComponent<RobotBreakApartEffect>();
            breakApart.visualRoot = visualCalibration;
            breakApart.maxFragments = number == 31 ? 32 : 28;
            breakApart.explosionForce = number == 31 ? 8.5f : 7.5f;
            target.RefreshGeometryCache();
            return SavePrefab(root, PrefabRoot + "/" + filename);
        }

        private static void UpgradeScene(Scene scene, GameplayPrefabs prefabs)
        {
            var manager = UnityEngine.Object.FindFirstObjectByType<GravityGameManager>();
            if (manager == null) throw new InvalidOperationException("Existing GravityGameManager is missing; refusing to rebuild user layout.");
            manager.scoutRobotPrefab = prefabs.robot29;
            manager.engineerRobotPrefab = prefabs.robot30;
            manager.guardRobotPrefab = prefabs.robot31;
            manager.healthPackPrefab = prefabs.health27;
            var designStart = manager.GetComponentInParent<GravityDesignStart>() ?? UnityEngine.Object.FindFirstObjectByType<GravityDesignStart>();
            if (designStart == null)
            {
                var origin = UnityEngine.Object.FindFirstObjectByType<XROrigin>();
                if (origin == null) throw new InvalidOperationException("XR Origin missing; cannot author design player start");
                designStart = origin.GetComponent<GravityDesignStart>() ?? origin.gameObject.AddComponent<GravityDesignStart>();
            }
            designStart.worldPosition = new Vector3(0f, 0f, 10.5f);
            designStart.worldYaw = 180f;
            designStart.eyeHeight = 2.136f;
            designStart.gameplayLandmark = GameObject.Find("SPAWN_POINTS_敌人出生点")?.transform;
            designStart.missionBoardOffset = new Vector3(-3.35f, 1.65f, 3.7f);
            designStart.ApplyToRig();
            manager.designStart = designStart;
            var fallback = designStart.GetComponent<DesktopFallbackController>();
            if (fallback != null) fallback.designStart = designStart;
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(designStart);
            UpgradeControllerVisual(manager.leftHand, false);
            UpgradeControllerVisual(manager.rightHand, true);

            EnforceScenePhysicsClassification(scene);
            var props = UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None).Where(t => t.kind == GravityTargetKind.Prop).ToArray();
            foreach (var prop in props)
            {
                prop.RefreshGeometryCache();
                prop.pullStagesRequired = prop.WorldBounds.extents.magnitude > 1.35f ? 2 : 1;
                prop.canBlockOnce = true;
                EditorUtility.SetDirty(prop);
            }

            ReplaceScenePreview(prefabs);
            UpgradeNightSky(manager.playerHead);
            TuneLighting();
            PolishHud(manager.hud);
            var smoke = manager.GetComponent<GravityAutomatedSmoke>() ?? manager.gameObject.AddComponent<GravityAutomatedSmoke>();
            smoke.game = manager;
            EditorUtility.SetDirty(smoke);
            var stability = manager.GetComponent<GravitySceneStabilityGuard>() ?? manager.gameObject.AddComponent<GravitySceneStabilityGuard>();
            stability.environmentRoot = GameObject.Find("ENVIRONMENT_欧式钟楼大道_可替换模型")?.transform;
            EditorUtility.SetDirty(stability);
            var evidence = manager.GetComponent<GravityEvidenceCapture>() ?? manager.gameObject.AddComponent<GravityEvidenceCapture>();
            evidence.game = manager;
            EditorUtility.SetDirty(evidence);
        }

        private static void EnforceScenePhysicsClassification(Scene scene)
        {
            var removedSynthetic = RemoveLegacySyntheticProps(scene);
            PromoteApprovedExistingSceneProps(scene);
            var approved = new List<GravityTarget>();
            var fixedCount = 0;
            var downgraded = 0;
            foreach (var target in UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                         .Where(t => t.kind == GravityTargetKind.Prop).ToArray())
            {
                NormalizeApprovedPropIdentity(target);
                if (IsApprovedLooseProp(target))
                {
                    approved.Add(target);
                    continue;
                }
                MakeFixedSceneObject(target.gameObject);
                downgraded++;
            }

            var environment = GameObject.Find("ENVIRONMENT_欧式钟楼大道_可替换模型");
            if (environment != null)
            {
                foreach (var body in environment.GetComponentsInChildren<Rigidbody>(true).ToArray())
                {
                    if (body == null || approved.Any(t => t != null && body.transform.IsChildOf(t.transform))) continue;
                    UnityEngine.Object.DestroyImmediate(body);
                    fixedCount++;
                }
                foreach (var item in environment.GetComponentsInChildren<Transform>(true))
                {
                    if (approved.Any(t => t != null && item.IsChildOf(t.transform))) continue;
                    item.gameObject.isStatic = true;
                }
            }

            Physics.SyncTransforms();
            foreach (var target in approved.ToArray())
            {
                if (target == null) continue;
                var overlappingColliders = FindInitialOverlapColliders(target, approved, includeTriggers: true, ignoreSafeTriggers: false);
                target.ConfigureSafeTriggerColliders(overlappingColliders.Length > 0
                    ? target.GetComponentsInChildren<Collider>(true)
                    : Array.Empty<Collider>());
                target.gameObject.isStatic = false;
                foreach (var item in target.GetComponentsInChildren<Transform>(true)) item.gameObject.isStatic = false;
                var body = target.GetComponent<Rigidbody>();
                if (body == null) body = target.gameObject.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                foreach (var collider in target.GetComponentsInChildren<Collider>(true))
                {
                    collider.enabled = true;
                    collider.isTrigger = target.IsSafeTriggerCollider(collider);
                }
                EditorUtility.SetDirty(target);
            }
            Debug.Log($"GRAVITY_SCENE_CLASSIFICATION_PASS dynamicSceneProps={approved.Count} removedSyntheticPlaceholders={removedSynthetic} downgraded={downgraded} removedFixedRigidbodies={fixedCount} fixedFloorAndArchitecture=true generatedPropVisuals=false userSceneModelsOnly=true");
        }

        private static void NormalizeApprovedPropIdentity(GravityTarget target)
        {
            if (target == null) return;
            if (GravityTarget.TryGetApprovedScenePropDisplayName(target.name, out var approvedName)) target.displayName = approvedName;
            EditorUtility.SetDirty(target);
        }

        private static int RemoveLegacySyntheticProps(Scene scene)
        {
            var synthetic = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item != null && (item.name.StartsWith("Prop_Crate_", StringComparison.Ordinal)
                                               || item.name.StartsWith("Prop_Bin_", StringComparison.Ordinal)
                                               || item.name.StartsWith("Prop_EnergyCanister_", StringComparison.Ordinal)))
                .Select(item => item.gameObject)
                .Distinct()
                .ToArray();
            foreach (var item in synthetic) UnityEngine.Object.DestroyImmediate(item);
            return synthetic.Length;
        }

        private static void PromoteApprovedExistingSceneProps(Scene scene)
        {
            var sceneProps = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(item => item != null && GravityTarget.TryGetApprovedScenePropDisplayName(item.name, out _))
                .Where(item => !item.GetComponentsInParent<GravityTarget>(true).Any(parent => parent != null && parent.kind == GravityTargetKind.Prop))
                .Where(item => !item.GetComponentsInParent<Transform>(true).Any(parent => parent != null && parent.name.StartsWith("SCENE_PREVIEW_ENEMIES_", StringComparison.Ordinal)))
                .Select(item => item.gameObject)
                .ToArray();
            foreach (var propObject in sceneProps)
            {
                if (!GravityTarget.TryGetApprovedScenePropDisplayName(propObject.name, out var approvedName)) continue;
                var visibleRenderer = propObject.GetComponentsInChildren<Renderer>(true)
                    .FirstOrDefault(renderer => renderer != null && renderer.enabled && renderer.sharedMaterial != null && renderer.bounds.extents.sqrMagnitude > .0004f);
                if (visibleRenderer == null) continue;
                var solidCollider = propObject.GetComponentsInChildren<Collider>(true)
                    .FirstOrDefault(collider => collider != null && collider.enabled && collider.bounds.extents.sqrMagnitude > .0004f);
                if (solidCollider == null) solidCollider = AddBoundsCollider(propObject);
                if (solidCollider == null) continue;
                foreach (var generated in propObject.GetComponentsInChildren<Transform>(true)
                             .Where(item => item != null && item.name.StartsWith("VISUAL_可吸道具_", StringComparison.Ordinal)).ToArray())
                    UnityEngine.Object.DestroyImmediate(generated.gameObject);
                var target = propObject.GetComponent<GravityTarget>() ?? propObject.AddComponent<GravityTarget>();
                target.kind = GravityTargetKind.Prop;
                target.displayName = approvedName;
                target.maxHealth = 1;
                target.ultimateCharge = 0;
                target.canBlockOnce = true;
                target.replaceableVisual = visibleRenderer.transform;
                var size = visibleRenderer.bounds.extents.magnitude;
                target.pullStagesRequired = size > 1.35f ? 2 : 1;
                target.heldFollowSharpness = 7.5f;
                target.heldStruggle = .035f;
                var body = propObject.GetComponent<Rigidbody>() ?? propObject.AddComponent<Rigidbody>();
                body.mass = Mathf.Clamp(size * 2.5f, 2f, 7.5f);
                body.isKinematic = true;
                body.useGravity = false;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
                propObject.isStatic = false;
                foreach (var child in propObject.GetComponentsInChildren<Transform>(true)) child.gameObject.isStatic = false;
                target.RefreshGeometryCache();
                EditorUtility.SetDirty(target);
                EditorUtility.SetDirty(body);
            }
        }

        private static Collider AddBoundsCollider(GameObject root)
        {
            if (root == null) return null;
            var bounds = CalculateBounds(root);
            if (bounds.extents.sqrMagnitude < .0004f) return null;
            var collider = root.AddComponent<BoxCollider>();
            var scale = root.transform.lossyScale;
            collider.center = root.transform.InverseTransformPoint(bounds.center);
            collider.size = new Vector3(
                Mathf.Max(.08f, bounds.size.x / Mathf.Max(.0001f, Mathf.Abs(scale.x))),
                Mathf.Max(.08f, bounds.size.y / Mathf.Max(.0001f, Mathf.Abs(scale.y))),
                Mathf.Max(.08f, bounds.size.z / Mathf.Max(.0001f, Mathf.Abs(scale.z))));
            collider.isTrigger = false;
            EditorUtility.SetDirty(collider);
            return collider;
        }

        private static bool IsApprovedLooseProp(GravityTarget target)
        {
            if (target == null || target.kind != GravityTargetKind.Prop) return false;
            if (!target.MatchesApprovedPropIdentity()) return false;
            return target.HasValidVisibleInteractionGeometry(out _);
        }

        private static void MakeFixedSceneObject(GameObject root)
        {
            if (root == null) return;
            foreach (var behaviour in root.GetComponentsInChildren<ChainReactionProp>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var target in root.GetComponentsInChildren<GravityTarget>(true)) UnityEngine.Object.DestroyImmediate(target);
            foreach (var body in root.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
            foreach (var collider in root.GetComponentsInChildren<Collider>(true))
            {
                collider.enabled = true;
                collider.isTrigger = false;
            }
            foreach (var item in root.GetComponentsInChildren<Transform>(true)) item.gameObject.isStatic = true;
            EditorUtility.SetDirty(root);
        }

        private static bool HasUnsafeInitialOverlap(GravityTarget target, IReadOnlyCollection<GravityTarget> approved)
        {
            return FindInitialOverlapColliders(target, approved, includeTriggers: false, ignoreSafeTriggers: true).Length > 0;
        }

        private static Collider[] FindInitialOverlapColliders(GravityTarget target, IReadOnlyCollection<GravityTarget> approved, bool includeTriggers, bool ignoreSafeTriggers)
        {
            if (target == null) return Array.Empty<Collider>();
            var own = target.GetComponentsInChildren<Collider>(true)
                .Where(c => c != null && c.enabled && (includeTriggers || !c.isTrigger) && (!ignoreSafeTriggers || !target.IsSafeTriggerCollider(c)))
                .ToArray();
            if (own.Length == 0) return Array.Empty<Collider>();
            var all = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
                .Where(c => c != null && c.enabled && (includeTriggers || !c.isTrigger))
                .ToArray();
            var overlapping = new HashSet<Collider>();
            foreach (var a in own)
            foreach (var b in all)
            {
                if (a == b || b.transform.IsChildOf(target.transform)) continue;
                var otherTarget = b.GetComponentInParent<GravityTarget>();
                if (otherTarget != null && otherTarget == target) continue;
                if (ignoreSafeTriggers && otherTarget != null && otherTarget.IsSafeTriggerCollider(b)) continue;
                if (!a.bounds.Intersects(b.bounds)) continue;
                if (!Physics.ComputePenetration(a, a.transform.position, a.transform.rotation, b, b.transform.position, b.transform.rotation, out _, out var distance)) continue;
                if (distance > .035f) overlapping.Add(a);
            }
            return overlapping.ToArray();
        }

        private static void ReplaceScenePreview(GameplayPrefabs prefabs)
        {
            var preview = GameObject.Find("SCENE_PREVIEW_ENEMIES_仅Scene查看");
            if (preview == null) preview = new GameObject("SCENE_PREVIEW_ENEMIES_仅Scene查看");
            for (var i = preview.transform.childCount - 1; i >= 0; i--) UnityEngine.Object.DestroyImmediate(preview.transform.GetChild(i).gameObject);
            Preview(prefabs.robot29, preview.transform, new Vector3(-4.6f, 0f, 1.8f), "预览_真实巡逻机29");
            Preview(prefabs.robot31, preview.transform, new Vector3(0f, 0f, -.3f), "预览_真实盾卫31");
            Preview(prefabs.robot30, preview.transform, new Vector3(4.6f, 0f, 1.8f), "预览_真实工程机30");
            preview.SetActive(true);
        }

        private static void UpgradeNightSky(Transform playerHead)
        {
            var old = GameObject.Find("SKY_原创程序化月夜_ange检索无合适许可资产");
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject("SKY_原创程序化月夜_ange检索无合适许可资产");
            var sky = root.AddComponent<GravityNightSky>();
            sky.playerHead = playerHead;
            var moon = GameObject.CreatePrimitive(PrimitiveType.Quad);
            moon.name = "Moon_月亮";
            UnityEngine.Object.DestroyImmediate(moon.GetComponent<Collider>());
            moon.transform.SetParent(root.transform, false);
            moon.transform.localPosition = new Vector3(-36f, 31f, -72f);
            moon.transform.localScale = Vector3.one * 8.2f;
            moon.GetComponent<Renderer>().sharedMaterial = TexturedUnlitMaterial("M_OriginalMoon", new Color(1f, .94f, .82f, 1f), GenerateMoonTexture(false), 3002);
            sky.moon = moon.transform;
            CreateMoonHalo(moon.transform, "MoonHalo_Inner_暖冷内晕", "M_OriginalMoonHaloInner", 1.5f, .025f, new Color(.54f, .64f, 1f, .3f), 3001);
            CreateMoonHalo(moon.transform, "MoonHalo_Outer_云雾外晕", "M_OriginalMoonHaloOuter", 2.05f, .05f, new Color(.24f, .34f, .82f, .12f), 3000);
            var starsGo = new GameObject("Stars_星点");
            starsGo.transform.SetParent(root.transform, false);
            var meshFilter = starsGo.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = GenerateStarFieldMesh();
            var renderer = starsGo.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = TexturedUnlitMaterial("M_OriginalStars", Color.white, GenerateStarTexture(), 2998);
            sky.stars = renderer;

            RenderSettings.skybox = CreateProceduralSkybox();
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.055f, .075f, .14f);
            RenderSettings.ambientEquatorColor = new Color(.025f, .035f, .075f);
            RenderSettings.ambientGroundColor = new Color(.008f, .012f, .025f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(.012f, .022f, .055f);
            RenderSettings.fogDensity = .006f;
        }

        private static void UpgradeControllerVisual(GravityHand hand, bool rightHand)
        {
            if (hand == null) throw new InvalidOperationException(rightHand ? "Right GravityHand missing" : "Left GravityHand missing");
            for (var i = hand.transform.childCount - 1; i >= 0; i--)
            {
                var child = hand.transform.GetChild(i);
                if (child.name.StartsWith("REPLACE_VISUAL_PICO_Controller_", StringComparison.Ordinal) ||
                    child.name.StartsWith("VISUAL_原创机械重力护腕_", StringComparison.Ordinal) ||
                    child.name == "GravityRayOrigin_能量发射口")
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
            }

            var sideName = rightHand ? "右_青" : "左_紫";
            var root = new GameObject("VISUAL_原创机械重力护腕_" + sideName);
            root.transform.SetParent(hand.transform, false);
            root.transform.localPosition = new Vector3(0f, -.015f, .015f);
            root.transform.localScale = Vector3.one * .68f;

            var shell = ControllerMaterial("M_Controller_Gunmetal", new Color(.11f, .15f, .21f), new Color(.015f, .035f, .07f));
            var shellLight = ControllerMaterial("M_Controller_Steel", new Color(.22f, .28f, .35f), new Color(.025f, .05f, .08f));
            var brass = ControllerMaterial("M_Controller_Brass", new Color(.62f, .4f, .16f), new Color(.16f, .07f, .015f));
            var rubber = ControllerMaterial("M_Controller_Rubber", new Color(.045f, .05f, .065f), Color.black);
            var coreColor = rightHand ? new Color(.12f, .76f, 1f) : new Color(.68f, .2f, 1f);
            var core = ControllerMaterial(rightHand ? "M_Controller_Core_Right" : "M_Controller_Core_Left", coreColor, coreColor * 3.5f);
            var warm = ControllerMaterial("M_Controller_ButtonWarm", new Color(1f, .5f, .12f), new Color(1f, .18f, .015f) * 2f);

            ControllerPart("主金属护壳", PrimitiveType.Cube, root.transform, new Vector3(0f, .005f, .035f), new Vector3(.18f, .105f, .27f), shell, Quaternion.identity);
            ControllerPart("上层装甲脊", PrimitiveType.Cube, root.transform, new Vector3(0f, .067f, .045f), new Vector3(.125f, .035f, .2f), shellLight, Quaternion.Euler(4f, 0f, 0f));
            ControllerPart("后部握把", PrimitiveType.Capsule, root.transform, new Vector3(0f, -.095f, -.085f), new Vector3(.067f, .115f, .067f), rubber, Quaternion.Euler(-18f, 0f, 0f));
            ControllerPart("腕部黄铜环", PrimitiveType.Cylinder, root.transform, new Vector3(0f, -.002f, -.115f), new Vector3(.14f, .025f, .14f), brass, Quaternion.Euler(90f, 0f, 0f));
            ControllerPart("前端黄铜护圈", PrimitiveType.Cylinder, root.transform, new Vector3(0f, .004f, .205f), new Vector3(.13f, .025f, .13f), brass, Quaternion.Euler(90f, 0f, 0f));
            ControllerPart("前端深色套筒", PrimitiveType.Cylinder, root.transform, new Vector3(0f, .004f, .232f), new Vector3(.105f, .032f, .105f), shell, Quaternion.Euler(90f, 0f, 0f));
            for (var side = -1; side <= 1; side += 2)
            {
                ControllerPart(side < 0 ? "左黄铜导轨" : "右黄铜导轨", PrimitiveType.Cube, root.transform, new Vector3(side * .102f, .004f, .035f), new Vector3(.024f, .075f, .225f), brass, Quaternion.identity);
                ControllerPart(side < 0 ? "左前护翼" : "右前护翼", PrimitiveType.Cube, root.transform, new Vector3(side * .092f, .018f, .185f), new Vector3(.038f, .095f, .075f), shellLight, Quaternion.Euler(0f, 0f, side * -12f));
            }
            ControllerPart("机械扳机", PrimitiveType.Cube, root.transform, new Vector3(0f, -.073f, .055f), new Vector3(.035f, .075f, .026f), brass, Quaternion.Euler(24f, 0f, 0f));
            ControllerPart("主技能按钮", PrimitiveType.Cylinder, root.transform, new Vector3(-.042f, .096f, .035f), new Vector3(.026f, .012f, .026f), core, Quaternion.identity);
            ControllerPart("副技能按钮", PrimitiveType.Cylinder, root.transform, new Vector3(.042f, .096f, .012f), new Vector3(.023f, .012f, .023f), warm, Quaternion.identity);
            var corePart = ControllerPart("重力能量核心_射线起点", PrimitiveType.Sphere, root.transform, new Vector3(0f, .004f, .285f), Vector3.one * .068f, core, Quaternion.identity);
            ControllerRing("前端能量发射环", root.transform, new Vector3(0f, .004f, .286f), .103f, .009f, core);
            ControllerRing("腕部状态环", root.transform, new Vector3(0f, -.002f, -.142f), .122f, .006f, brass);

            var rayOrigin = new GameObject("GravityRayOrigin_能量发射口").transform;
            rayOrigin.SetParent(corePart.transform, false);
            rayOrigin.localPosition = new Vector3(0f, 0f, .058f);
            hand.rayOrigin = rayOrigin;
            hand.handGlow = corePart.GetComponent<Renderer>();
            EditorUtility.SetDirty(hand);
        }

        private static GameObject ControllerPart(string name, PrimitiveType type, Transform parent, Vector3 position, Vector3 scale, Material material, Quaternion rotation)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            UnityEngine.Object.DestroyImmediate(part.GetComponent<Collider>());
            part.GetComponent<Renderer>().sharedMaterial = material;
            return part;
        }

        private static void ControllerRing(string name, Transform parent, Vector3 position, float radius, float width, Material material)
        {
            const int segments = 36;
            var ring = new GameObject(name);
            ring.transform.SetParent(parent, false);
            ring.transform.localPosition = position;
            var line = ring.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = segments;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.sharedMaterial = material;
            for (var i = 0; i < segments; i++)
            {
                var angle = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
            }
        }

        private static Material ControllerMaterial(string name, Color color, Color emission)
        {
            var material = RuntimeMaterial(name, color, emission);
            material.SetFloat("_Metallic", name.Contains("Rubber", StringComparison.Ordinal) ? .05f : .68f);
            material.SetFloat("_Glossiness", name.Contains("Rubber", StringComparison.Ordinal) ? .32f : .82f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ValidateController(GravityHand hand, string side, List<string> errors)
        {
            if (hand == null) { errors.Add($"{side} GravityHand missing"); return; }
            if (hand.rayOrigin == null || !hand.rayOrigin.name.StartsWith("GravityRayOrigin_", StringComparison.Ordinal)) errors.Add($"{side} controller ray origin missing");
            if (hand.handGlow == null) errors.Add($"{side} controller energy core missing");
            if (hand.GetComponentsInChildren<Transform>(true).Any(t => t.name == "ControllerPlaceholder")) errors.Add($"{side} controller placeholder remains");
            var visual = hand.transform.Cast<Transform>().FirstOrDefault(t => t.name.StartsWith("VISUAL_原创机械重力护腕_", StringComparison.Ordinal));
            if (visual == null || visual.GetComponentsInChildren<Renderer>(true).Length < 10) errors.Add($"{side} original mechanical controller visual incomplete");
        }

        private static void TuneLighting()
        {
            foreach (var light in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Point)
                {
                    light.color = Color.Lerp(light.color, Warm, .58f);
                    light.intensity = Mathf.Clamp(light.intensity, 1.2f, 4.8f);
                    light.range = Mathf.Clamp(light.range, 5f, 12f);
                    light.shadows = LightShadows.None;
                }
                else if (light.type == LightType.Directional)
                {
                    light.color = new Color(.42f, .55f, .82f);
                    light.intensity = Mathf.Min(light.intensity, .72f);
                    light.shadows = LightShadows.Soft;
                }
                EditorUtility.SetDirty(light);
            }
        }

        private static void PolishHud(GravityHud hud)
        {
            if (hud == null) return;
            var canvas = hud.GetComponent<Canvas>();
            if (canvas != null)
            {
                hud.transform.SetParent(null, false);
                canvas.renderMode = RenderMode.WorldSpace;
                canvas.worldCamera = Camera.main;
                canvas.overrideSorting = true;
                canvas.sortingOrder = 20;
                canvas.planeDistance = 1f;
                var scaler = hud.GetComponent<CanvasScaler>();
                if (scaler != null)
                {
                    scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
                    scaler.dynamicPixelsPerUnit = 1f;
                    EditorUtility.SetDirty(scaler);
                }
                if (hud.transform is RectTransform rect)
                {
                    rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
                    rect.pivot = new Vector2(.5f, .5f);
                    rect.anchoredPosition = Vector2.zero;
                    rect.sizeDelta = new Vector2(1200f, 760f);
                }
                var designStart = UnityEngine.Object.FindFirstObjectByType<GravityDesignStart>();
                if (designStart != null)
                {
                    hud.transform.SetPositionAndRotation(designStart.MissionBoardPosition, designStart.MissionBoardRotation);
                    hud.transform.localScale = Vector3.one * .0034f;
                    if (hud.transform is RectTransform boardRect)
                        boardRect.anchoredPosition3D = designStart.MissionBoardPosition;
                }
                EditorUtility.SetDirty(canvas);
                EditorUtility.SetDirty(hud.transform);
            }
            var existingPanel = hud.transform.Find("HUD_Backplate_XRReadable");
            var panel = existingPanel != null ? existingPanel.gameObject : new GameObject("HUD_Backplate_XRReadable", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(hud.transform, false);
            panel.transform.SetAsFirstSibling();
            var panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = panelRect.anchorMax = new Vector2(.5f, .5f);
            panelRect.pivot = new Vector2(.5f, .5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(1160f, 700f);
            var panelImage = panel.GetComponent<Image>();
            panelImage.color = new Color(.015f, .025f, .06f, .82f);
            panel.SetActive(true);
            panelImage.raycastTarget = false;
            foreach (var label in hud.GetComponentsInChildren<Text>(true))
            {
                var color = label == hud.statusText ? new Color(1f, .76f, .3f, 1f) : new Color(.84f, .94f, 1f, 1f);
                label.color = color;
                label.raycastTarget = false;
                var outline = label.GetComponent<Outline>() ?? label.gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(.015f, .02f, .055f, .92f);
                outline.effectDistance = new Vector2(1f, -1f);
                EditorUtility.SetDirty(label);
            }
            foreach (var image in hud.GetComponentsInChildren<Image>(true))
            {
                image.raycastTarget = false;
                EditorUtility.SetDirty(image);
            }
            var raycaster = hud.GetComponent<GraphicRaycaster>();
            if (raycaster != null) raycaster.enabled = false;
        }

        private static Material CreateProceduralSkybox()
        {
            var path = MaterialRoot + "/M_OriginalProceduralNightSky.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.SetFloat("_SunSize", .008f);
            material.SetFloat("_SunSizeConvergence", 8f);
            material.SetFloat("_AtmosphereThickness", .32f);
            material.SetColor("_SkyTint", new Color(.025f, .055f, .16f));
            material.SetColor("_GroundColor", new Color(.008f, .012f, .025f));
            material.SetFloat("_Exposure", .42f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void ConfigureProjectAndPico()
        {
            PlayerSettings.productName = "引力 斥力";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gravitywarden.repulsion");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.MTRendering = true;
            QualitySettings.antiAliasing = 2;
            QualitySettings.pixelLightCount = 4;
            SetInputHandler(1);
            var xr = XRGeneralSettingsPerBuildTarget.XRGeneralSettingsForBuildTarget(BuildTargetGroup.Android);
            if (xr?.AssignedSettings == null) throw new InvalidOperationException("Android XR Manager settings missing");
            if (!xr.AssignedSettings.activeLoaders.Any(l => l != null && l.GetType().FullName == LoaderType))
                XRPackageMetadataStore.AssignLoader(xr.AssignedSettings, LoaderType, BuildTargetGroup.Android);
            var pxr = PXR_Settings.GetSettings() ?? throw new InvalidOperationException("PXR Settings missing");
            pxr.appMode = PXR_Settings.AppMode.XR;
            pxr.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.Multiview;
            pxr.optimizeBufferDiscards = true;
            EditorUtility.SetDirty(pxr);
        }

        private static void ValidatePrefab(string path, GravityTargetKind kind, List<string> errors, int sourceAsset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) { errors.Add("Missing prefab " + path); return; }
            var target = prefab.GetComponent<GravityTarget>();
            if (target == null || target.kind != kind) errors.Add("Invalid GravityTarget in " + path);
            var expectedPath = AssetDatabase.GetAssetPath(LoadNumberedModel(sourceAsset));
            var dependencies = AssetDatabase.GetDependencies(path, true);
            if (!dependencies.Contains(expectedPath)) errors.Add($"Real source model {sourceAsset} missing in {path}");
        }

        private static void ValidateProjectileFacing(string path, List<string> errors, int sourceAsset)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var projectile = prefab != null ? prefab.GetComponent<EnemyProjectile>() : null;
            if (projectile == null || projectile.visualForward == null || projectile.modelRotationOffset.sqrMagnitude > .001f
                || Vector3.Dot(projectile.modelForwardAxis.normalized, Vector3.up) < .999f)
                errors.Add($"Projectile {sourceAsset} forward configuration invalid");
            var visualHeight = projectile?.visualForward != null ? CalculateBounds(projectile.visualForward.gameObject).size.y : 0f;
            var collider = prefab != null ? prefab.GetComponent<SphereCollider>() : null;
            var glow = prefab != null ? prefab.GetComponent<Light>() : null;
            var trail = prefab != null ? prefab.GetComponent<TrailRenderer>() : null;
            if (visualHeight < .68f || visualHeight > .72f) errors.Add($"Projectile {sourceAsset} visual height outside 0.68-0.72m: {visualHeight:0.000}");
            if (collider == null || collider.radius < .29f || collider.radius > .31f) errors.Add($"Projectile {sourceAsset} collider does not match the enlarged visual");
            if (glow == null || glow.type != LightType.Point || glow.intensity < 1.5f || glow.range < 2.5f) errors.Add($"Projectile {sourceAsset} emissive point glow missing or too weak");
            var trailEmission = trail != null && trail.sharedMaterial != null && trail.sharedMaterial.HasProperty("_EmissionColor")
                ? trail.sharedMaterial.GetColor("_EmissionColor").maxColorComponent
                : 0f;
            if (trail == null || trail.sharedMaterial == null || trailEmission < 1f) errors.Add($"Projectile {sourceAsset} emissive trail missing");
            if (projectile != null && projectile.visualForward != null)
                Debug.Log($"GRAVITY_PROJECTILE_FACING_PASS asset={sourceAsset} visual={projectile.visualForward.name} noseAxis={projectile.modelForwardAxis} offset={projectile.modelRotationOffset} height={visualHeight:0.000} colliderRadius={collider?.radius:0.000} glowIntensity={glow?.intensity:0.00}");
        }

        private static void ValidateHealthFacing(string path, List<string> errors)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            var target = prefab != null ? prefab.GetComponent<GravityTarget>() : null;
            var billboard = prefab != null ? prefab.GetComponent<FacePlayerBillboard>() : null;
            var yaw = target?.replaceableVisual != null ? target.replaceableVisual.localEulerAngles.y : float.NaN;
            if (float.IsNaN(yaw) || Mathf.Abs(Mathf.DeltaAngle(yaw, 90f)) > .1f || billboard == null || billboard.visualRoot != target.replaceableVisual || Mathf.Abs(Mathf.DeltaAngle(billboard.yawOffset, 90f)) > .1f)
                errors.Add("Health pack 27 visual must present the cross-marked model front toward the camera at Y=90");
            else Debug.Log($"GRAVITY_HEALTH_FACING_PASS asset=27 visual={target.replaceableVisual.name} localEuler={target.replaceableVisual.localEulerAngles}");
        }

        private static void ValidateRobot(string path, List<string> errors, int sourceAsset)
        {
            ValidatePrefab(path, GravityTargetKind.Robot, errors, sourceAsset);
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                if (contents.GetComponent<RobotEnemy>() == null) errors.Add("RobotEnemy missing in " + path);
                if (contents.GetComponent<RobotBreakApartEffect>() == null) errors.Add("RobotBreakApartEffect missing in " + path);
                var motion = contents.GetComponent<RobotRigMotion>();
                if (motion == null) errors.Add("RobotRigMotion missing in " + path);
                var enemy = contents.GetComponent<RobotEnemy>();
                if (enemy != null)
                {
                    if (Mathf.Abs(Mathf.DeltaAngle(enemy.visualYawOffset, 180f)) > .1f) errors.Add($"Robot {sourceAsset} visualYawOffset must be 180, found {enemy.visualYawOffset}");
                    if (enemy.visualRoot == null || Mathf.Abs(Mathf.DeltaAngle(enemy.visualRoot.localEulerAngles.y, 180f)) > .1f)
                        errors.Add($"Robot {sourceAsset} visualRoot local Y must equal 180");
                    else if (Vector3.Dot(enemy.VisualFacingDirection, -contents.transform.forward) < .99f)
                        errors.Add($"Robot {sourceAsset} visual face axis is not aligned with logic forward");
                    else Debug.Log($"GRAVITY_ROBOT_FACING_PASS asset={sourceAsset} gameplayRoot=dynamic-look-at visualNode={enemy.visualRoot.name} localEuler={enemy.visualRoot.localEulerAngles} visualYawOffset={enemy.visualYawOffset:0.0} separated=true");
                }
                var renderers = contents.GetComponentsInChildren<Renderer>(true).Where(r => r is MeshRenderer || r is SkinnedMeshRenderer).ToArray();
                var meshFilters = contents.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh != null);
                var skinned = contents.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(m => m.sharedMesh != null);
                var detachable = CountDetachableParts(contents.transform);
                if (renderers.Length < 8 || meshFilters + skinned < 8 || detachable < 8)
                    errors.Add($"Robot {sourceAsset} split geometry invalid in {path}: renderers={renderers.Length} meshes={meshFilters + skinned} detachable={detachable}");
                if (motion != null)
                {
                    motion.DebugSetPose(.25f, true, false);
                    if (!motion.HasRig || motion.CurrentWalkBoneAngle < 12f) errors.Add($"Robot {sourceAsset} procedural rig mapping/walk invalid");
                    else Debug.Log($"GRAVITY_RIG_ASSET_PASS asset={sourceAsset} sourceClips=none strategy=procedural_bound_bones rig={motion.HasRig} walkAngle={motion.CurrentWalkBoneAngle:0.0}");
                }
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
        }

        private static string GeometryStats(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true).Count(r => r is MeshRenderer || r is SkinnedMeshRenderer);
            var meshFilters = root.GetComponentsInChildren<MeshFilter>(true).Count(m => m.sharedMesh != null);
            var skinned = root.GetComponentsInChildren<SkinnedMeshRenderer>(true).Count(m => m.sharedMesh != null);
            var lod = root.GetComponentsInChildren<LODGroup>(true).Length;
            var detachable = CountDetachableParts(root.transform);
            return $"renderers={renderers},meshFilters={meshFilters},skinned={skinned},lod={lod},detachable={detachable},transforms={root.GetComponentsInChildren<Transform>(true).Length}";
        }

        private static int CountDetachableParts(Transform root)
        {
            var count = 0;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer is not MeshRenderer && renderer is not SkinnedMeshRenderer) continue;
                var candidate = renderer.transform;
                while (candidate.parent != null && candidate.parent != root && candidate.parent.GetComponentsInChildren<Renderer>(true).Count(r => r is MeshRenderer || r is SkinnedMeshRenderer) == 1)
                    candidate = candidate.parent;
                count++;
            }
            return count;
        }

        private static GameObject LoadNumberedModel(int number)
        {
            var guid = AssetDatabase.FindAssets("t:Model", new[] { AssetRoot + "/" + number }).FirstOrDefault();
            if (string.IsNullOrEmpty(guid)) throw new FileNotFoundException("Model FBX missing for asset " + number);
            return AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
        }

        private static void NormalizeModel(Transform model, float targetLargestOrHeight, bool centerOnRoot)
        {
            model.localPosition = Vector3.zero;
            var bounds = CalculateBounds(model.gameObject);
            var sourceSize = bounds.size.y > .001f ? bounds.size.y : bounds.size.magnitude;
            var scale = targetLargestOrHeight / Mathf.Max(.001f, sourceSize);
            model.localScale *= scale;
            bounds = CalculateBounds(model.gameObject);
            if (centerOnRoot)
            {
                var parent = model.parent;
                var localCenter = parent.InverseTransformPoint(bounds.center);
                var localBottom = parent.InverseTransformPoint(new Vector3(bounds.center.x, bounds.min.y, bounds.center.z));
                model.localPosition += new Vector3(-localCenter.x, -localBottom.y, -localCenter.z);
            }
        }

        private static Bounds CalculateBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return new Bounds(root.transform.position, Vector3.one);
            var bounds = renderers[0].bounds;
            for (var i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);
            return bounds;
        }

        private static int ParseAssetNumber(string assetPath)
        {
            var relative = assetPath.Substring(AssetRoot.Length).TrimStart('/');
            return int.TryParse(relative.Split('/')[0], out var result) ? result : -1;
        }

        private static string HierarchyPath(Transform item)
        {
            var names = new Stack<string>();
            for (var current = item; current != null; current = current.parent) names.Push(current.name);
            return string.Join("/", names);
        }

        private static void Preview(GameObject prefab, Transform parent, Vector3 position, string name)
        {
            var gameplayInstance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            PrefabUtility.UnpackPrefabInstance(gameplayInstance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            var enemy = gameplayInstance.GetComponent<RobotEnemy>() ?? throw new InvalidOperationException($"Preview source {prefab.name} has no RobotEnemy");
            var visual = enemy.visualRoot ?? throw new InvalidOperationException($"Preview source {prefab.name} has no visual root");
            visual.SetParent(null, false);
            UnityEngine.Object.DestroyImmediate(gameplayInstance);

            var instance = new GameObject(name);
            instance.transform.SetParent(parent, false);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = RobotEnemy.RootRotationFacing(new Vector3(0f, 1.8f, 10.5f) - position);
            visual.SetParent(instance.transform, false);
            foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(behaviour);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(collider);
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) UnityEngine.Object.DestroyImmediate(body);
        }

        private static GameObject StageForCapture(GameObject prefab, Vector3 position, Vector3 facePoint)
        {
            if (prefab == null) return null;
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "CAPTURE_ONLY_" + prefab.name;
            instance.transform.position = position;
            var direction = facePoint - position;
            direction.y = 0f;
            if (direction.sqrMagnitude > .01f) instance.transform.rotation = RobotEnemy.RootRotationFacing(direction);
            foreach (var behaviour in instance.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour is FacePlayerBillboard) continue;
                behaviour.enabled = false;
            }
            foreach (var body in instance.GetComponentsInChildren<Rigidbody>(true)) body.isKinematic = true;
            return instance;
        }

        private static void CreateLabel(string text, Vector3 position)
        {
            var label = new GameObject("Label_" + text).AddComponent<TextMesh>();
            label.text = text;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.fontSize = 52;
            label.characterSize = .06f;
            label.color = Color.white;
            label.transform.position = position;
        }

        private static Material RuntimeMaterial(string name, Color color, Color emission)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(Shader.Find("Standard"));
                AssetDatabase.CreateAsset(material, path);
            }
            material.color = color;
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission);
            material.SetFloat("_Metallic", .12f);
            material.SetFloat("_Glossiness", .68f);
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material HealthParticleMaterial()
        {
            const string name = "M_HealthPackGreenParticles_27";
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Legacy Shaders/Particles/Additive") ?? Shader.Find("Unlit/Color");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (shader != null && material.shader != shader) material.shader = shader;
            var green = new Color(.18f, 1f, .48f, .9f);
            if (material.HasProperty("_Color")) material.SetColor("_Color", green);
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", green);
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", green * 4f);
            material.EnableKeyword("_EMISSION");
            material.renderQueue = 3100;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material TexturedUnlitMaterial(string name, Color color, Texture texture, int renderQueue)
        {
            var path = MaterialRoot + "/" + name + ".mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            var shader = Shader.Find("GravityRepulsion/UnlitTransparent") ?? throw new InvalidOperationException("Gravity transparent shader missing");
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader) material.shader = shader;
            material.SetColor("_Color", color);
            material.mainTexture = texture;
            if (material.HasProperty("_Cull")) material.SetFloat("_Cull", 0f);
            material.renderQueue = renderQueue;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void CreateMoonHalo(Transform moon, string objectName, string materialName, float scale, float depth, Color color, int renderQueue)
        {
            var halo = GameObject.CreatePrimitive(PrimitiveType.Quad);
            halo.name = objectName;
            UnityEngine.Object.DestroyImmediate(halo.GetComponent<Collider>());
            halo.transform.SetParent(moon, false);
            halo.transform.localPosition = new Vector3(0f, 0f, depth);
            halo.transform.localScale = Vector3.one * scale;
            halo.GetComponent<Renderer>().sharedMaterial = TexturedUnlitMaterial(materialName, color, GenerateMoonTexture(true), renderQueue);
        }

        private static Mesh GenerateStarFieldMesh()
        {
            var path = MaterialRoot + "/MESH_OriginalStarFieldV3.asset";
            var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;
            const int count = 960;
            var random = new System.Random(20260814);
            var vertices = new Vector3[count * 4];
            var uvs = new Vector2[count * 4];
            var colors = new Color[count * 4];
            var triangles = new int[count * 6];
            for (var i = 0; i < count; i++)
            {
                var azimuth = Mathf.Lerp(-Mathf.PI, Mathf.PI, (float)random.NextDouble());
                var elevation = Mathf.Lerp(.03f, 1.25f, Mathf.Pow((float)random.NextDouble(), .72f));
                var radius = Mathf.Lerp(62f, 74f, (float)random.NextDouble());
                var horizontal = Mathf.Cos(elevation);
                var direction = new Vector3(Mathf.Sin(azimuth) * horizontal, Mathf.Sin(elevation), Mathf.Cos(azimuth) * horizontal).normalized;
                var center = direction * radius;
                var normal = -direction;
                var right = Vector3.Cross(Vector3.up, normal).normalized;
                if (right.sqrMagnitude < .5f) right = Vector3.right;
                var up = Vector3.Cross(normal, right).normalized;
                var size = Mathf.Lerp(.2f, .4f, Mathf.Pow((float)random.NextDouble(), 2.15f));
                var vertex = i * 4;
                vertices[vertex] = center - right * size - up * size;
                vertices[vertex + 1] = center + right * size - up * size;
                vertices[vertex + 2] = center + right * size + up * size;
                vertices[vertex + 3] = center - right * size + up * size;
                uvs[vertex] = Vector2.zero;
                uvs[vertex + 1] = Vector2.right;
                uvs[vertex + 2] = Vector2.one;
                uvs[vertex + 3] = Vector2.up;
                var color = Color.Lerp(new Color(.56f, .72f, 1f, .8f), new Color(1f, .92f, .72f, .98f), (float)random.NextDouble());
                for (var c = 0; c < 4; c++) colors[vertex + c] = color;
                var triangle = i * 6;
                triangles[triangle] = vertex;
                triangles[triangle + 1] = vertex + 2;
                triangles[triangle + 2] = vertex + 1;
                triangles[triangle + 3] = vertex;
                triangles[triangle + 4] = vertex + 3;
                triangles[triangle + 5] = vertex + 2;
            }
            var mesh = new Mesh { name = "MESH_OriginalStarField" };
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        private static Texture2D GenerateStarTexture()
        {
            var path = MaterialRoot + "/T_OriginalStarSoft.png";
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var uv = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                var radial = Mathf.Exp(-uv.sqrMagnitude * 7.5f);
                var cross = Mathf.Exp(-Mathf.Abs(uv.x) * 20f) * Mathf.Exp(-Mathf.Abs(uv.y) * 2.8f)
                          + Mathf.Exp(-Mathf.Abs(uv.y) * 20f) * Mathf.Exp(-Mathf.Abs(uv.x) * 2.8f);
                var alpha = Mathf.Clamp01(radial + cross * .16f) * (1f - Mathf.SmoothStep(.72f, 1f, uv.magnitude));
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Texture2D GenerateMoonTexture(bool halo)
        {
            var filename = halo ? "T_OriginalMoonHaloV2.png" : "T_OriginalMoonSurfaceV2.png";
            var path = MaterialRoot + "/" + filename;
            var existing = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (existing != null) return existing;
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            var pixels = new Color[size * size];
            for (var y = 0; y < size; y++)
            for (var x = 0; x < size; x++)
            {
                var uv = new Vector2((x + .5f) / size * 2f - 1f, (y + .5f) / size * 2f - 1f);
                var radius = uv.magnitude;
                if (halo)
                {
                    var cloud = Mathf.PerlinNoise(x * .025f + 2.7f, y * .025f + 9.1f);
                    var radial = 1f - Mathf.SmoothStep(.18f, 1f, radius);
                    var alpha = radial * Mathf.Lerp(.38f, .85f, cloud) * .34f;
                    pixels[y * size + x] = new Color(.55f, .66f, 1f, alpha);
                }
                else
                {
                    var edge = 1f - Mathf.SmoothStep(.82f, 1f, radius);
                    var broad = Mathf.PerlinNoise(x * .016f + 4.1f, y * .016f + 1.7f);
                    var fine = Mathf.PerlinNoise(x * .071f + 8.4f, y * .071f + 6.2f);
                    var craterA = Mathf.SmoothStep(.23f, .02f, Vector2.Distance(uv, new Vector2(-.28f, .18f)));
                    var craterB = Mathf.SmoothStep(.18f, .018f, Vector2.Distance(uv, new Vector2(.3f, -.16f)));
                    var craterC = Mathf.SmoothStep(.12f, .014f, Vector2.Distance(uv, new Vector2(.08f, .34f)));
                    var mare = Mathf.SmoothStep(.67f, .25f, Vector2.Distance(uv, new Vector2(-.12f, -.08f))) * broad;
                    var shade = Mathf.Clamp01(.72f + broad * .3f + fine * .11f - (craterA + craterB + craterC) * .32f - mare * .16f - uv.x * .08f);
                    pixels[y * size + x] = new Color(shade, shade * .93f, shade * .82f, edge);
                }
            }
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = halo;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static GameObject SavePrefab(GameObject source, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            return prefab;
        }

        private static void BackupUserLayout()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(BackupPath) != null) return;
            if (!AssetDatabase.CopyAsset(ScenePath, BackupPath)) throw new IOException("Could not back up user layout scene");
            AssetDatabase.ImportAsset(BackupPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void EnsureFolders()
        {
            foreach (var folder in new[] { "Assets/GravityWarden", "Assets/GravityWarden/Scenes", PrefabRoot, MaterialRoot, "Assets/GravityWarden/Shaders", "Assets/GravityWarden/Documentation" })
            {
                if (AssetDatabase.IsValidFolder(folder)) continue;
                var split = folder.LastIndexOf('/');
                AssetDatabase.CreateFolder(folder.Substring(0, split), folder.Substring(split + 1));
            }
        }

        private static Text TitleText(string name, Transform parent, Vector2 position, Vector2 size, string content, int fontSize, Color color, TextAnchor alignment)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f);
            rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static void ValidateIsolatedTitleScene(List<string> errors)
        {
            if (!File.Exists(TitleScenePath))
            {
                errors.Add("Isolated title scene is missing");
                return;
            }

            var previous = SceneManager.GetActiveScene().path;
            try
            {
                var titleScene = EditorSceneManager.OpenScene(TitleScenePath, OpenSceneMode.Single);
                var title = UnityEngine.Object.FindFirstObjectByType<GravityTitleController>();
                var titleStart = UnityEngine.Object.FindFirstObjectByType<GravityDesignStart>();
                var gameplayObjects = UnityEngine.Object.FindObjectsByType<GravityGameManager>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                if (title == null) errors.Add("Title controller missing from isolated title scene");
                if (titleStart == null || !titleStart.MatchesRig()) errors.Add("Title phase has no authoritative design player start");
                if (gameplayObjects.Length != 0 || GameObject.Find("GAMEPLAY_玩法与出生点") != null)
                    errors.Add("Title scene contains gameplay objects");
                var canvas = UnityEngine.Object.FindFirstObjectByType<Canvas>();
                var cover = GameObject.Find("OPAQUE_TITLE_COVER")?.GetComponent<Image>();
                if (canvas == null || canvas.renderMode != RenderMode.ScreenSpaceCamera || canvas.sortingOrder < 1000 || cover == null || cover.color.a < .999f)
                    errors.Add("Title scene lacks an opaque standalone cover");
                if (titleScene.GetRootGameObjects().Any(root => root.name.Contains("GAMEPLAY") || root.name.Contains("MISSION_BOARD")))
                    errors.Add("Title scene root indicates gameplay or gameplay HUD contamination");
                Debug.Log($"GRAVITY_TITLE_ISOLATION_PASS scene={titleScene.path} gameplayLoaded=false gameplayManagers={gameplayObjects.Length} opaqueCover={(cover != null && cover.color.a >= .999f)} titleStart=({titleStart?.worldPosition})/{titleStart?.worldYaw:0.0}/{titleStart?.eyeHeight:0.000}");
            }
            finally
            {
                if (!string.IsNullOrEmpty(previous) && File.Exists(previous)) EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
            }
        }

        private static void SetInputHandler(int value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            var serialized = new SerializedObject(assets[0]);
            var property = serialized.FindProperty("activeInputHandler") ?? throw new InvalidOperationException("activeInputHandler setting missing");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int ReadInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            return assets.Length == 0 ? -1 : new SerializedObject(assets[0]).FindProperty("activeInputHandler")?.intValue ?? -1;
        }

        private static void Build(BuildTarget target, string output, BuildOptions options)
        {
            var scenes = EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path).ToArray();
            if (scenes.Length < 2 || scenes[0] != TitleScenePath || !scenes.Contains(ScenePath))
                throw new BuildFailedException("Build scenes must start with the isolated title scene followed by gameplay");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions { scenes = scenes, locationPathName = output, target = target, options = options });
            if (report.summary.result != BuildResult.Succeeded) throw new BuildFailedException($"Build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"GRAVITY_CURRENT_LAYOUT_BUILD_PASS target={target} output={output} size={report.summary.totalSize} titleScene=true gameplayScene=true");
        }
    }
}
