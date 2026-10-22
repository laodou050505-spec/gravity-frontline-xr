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
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEngine.XR.Management;

namespace GravityRepulsion.Editor
{
    public static class GravityProjectBuilder
    {
        private const string Root = "Assets/GravityWarden";
        private const string ScenePath = Root + "/Scenes/GravityBoulevard.unity";
        private const string LoaderType = "ByteDance.PICO.XR.PXR_Loader";
        private const string PicoSettingsKey = "ByteDance.PICO.XR.Settings";
        private const string MainSceneOpenedSessionKey = "GravityRepulsion.MainSceneOpened";

        private static readonly Color Night = new(.022f, .045f, .085f);
        private static readonly Color Stone = new(.12f, .18f, .24f);
        private static readonly Color StoneLight = new(.21f, .29f, .35f);
        private static readonly Color Brass = new(.48f, .37f, .2f);
        private static readonly Color Violet = new(.61f, .23f, 1f);
        private static readonly Color Cyan = new(.18f, .78f, 1f);
        private static readonly Color Threat = new(1f, .12f, .26f);
        private static readonly Color Warm = new(1f, .54f, .2f);
        private static Dictionary<string, Material> mats;
        private static double mainSceneOpenFallbackTime;

        [InitializeOnLoadMethod]
        private static void QueueGameplaySceneOnEditorLaunch()
        {
            if (Application.isBatchMode || SessionState.GetBool(MainSceneOpenedSessionKey, false)) return;

            mainSceneOpenFallbackTime = EditorApplication.timeSinceStartup + 10d;
            EditorSceneManager.sceneOpened -= HandleEditorSceneOpened;
            EditorSceneManager.sceneOpened += HandleEditorSceneOpened;
            EditorApplication.update -= EnsureGameplaySceneAfterStartup;
            EditorApplication.update += EnsureGameplaySceneAfterStartup;
        }

        private static void HandleEditorSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (scene.path.StartsWith("Assets/", StringComparison.Ordinal))
            {
                if (scene.path == ScenePath) EditorApplication.delayCall += FrameSceneForHandoff;
                MarkMainSceneHandled();
                return;
            }

            EditorApplication.delayCall += OpenGameplaySceneIfNeeded;
        }

        private static void EnsureGameplaySceneAfterStartup()
        {
            if (EditorApplication.timeSinceStartup < mainSceneOpenFallbackTime) return;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;

            OpenGameplaySceneIfNeeded();
        }

        private static void OpenGameplaySceneIfNeeded()
        {
            if (Application.isBatchMode || EditorApplication.isPlayingOrWillChangePlaymode) return;
            if (SessionState.GetBool(MainSceneOpenedSessionKey, false))
            {
                StopWatchingForMainScene();
                return;
            }

            var activePath = SceneManager.GetActiveScene().path;
            if (activePath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                MarkMainSceneHandled();
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(ScenePath) == null) return;

            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            MarkMainSceneHandled();
        }

        private static void MarkMainSceneHandled()
        {
            SessionState.SetBool(MainSceneOpenedSessionKey, true);
            StopWatchingForMainScene();
        }

        private static void StopWatchingForMainScene()
        {
            EditorSceneManager.sceneOpened -= HandleEditorSceneOpened;
            EditorApplication.update -= EnsureGameplaySceneAfterStartup;
        }

        [MenuItem("GravityFrontline/Locate Scene Placement View")]
        public static void FrameSceneForHandoff()
        {
            if (EditorSceneManager.GetActiveScene().path != ScenePath)
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            Selection.activeObject = null;
            var sceneView = SceneView.lastActiveSceneView;
            if (sceneView == null) return;

            sceneView.in2DMode = false;
            sceneView.pivot = new Vector3(0f, 4.5f, -40f);
            sceneView.rotation = Quaternion.Euler(24f, 180f, 0f);
            sceneView.size = 46f;
            sceneView.Repaint();
        }

        [MenuItem("GravityFrontline/Generate Full PICO XR Scene")]
        public static void GenerateCompleteProject()
        {
            GravityExistingSceneUpgrader.UpgradeExistingScene();
        }

        public static void BatchGenerate() => GenerateCompleteProject();

        [MenuItem("GravityFrontline/Validate Project & PICO Config")]
        public static void BatchValidate()
        {
            GravityExistingSceneUpgrader.ValidateExistingScene();
        }

        [MenuItem("GravityFrontline/Build macOS Desktop Test")]
        public static void BatchBuildMac()
        {
            GravityExistingSceneUpgrader.BuildMac();
        }

        [MenuItem("GravityFrontline/Build PICO Android APK")]
        public static void BatchBuildAndroid()
        {
            GravityExistingSceneUpgrader.BuildAndroid();
        }

        [MenuItem("GravityFrontline/Capture Scene Composition Preview")]
        public static void BatchCapturePreview()
        {
            GravityExistingSceneUpgrader.CaptureGameView();
        }

        private static void BuildScene(PrefabSet prefabs)
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "GravityBoulevard";
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(.16f, .24f, .36f);
            RenderSettings.ambientEquatorColor = new Color(.08f, .11f, .17f);
            RenderSettings.ambientGroundColor = new Color(.018f, .025f, .04f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Night;
            RenderSettings.fogDensity = .011f;

            var environment = New("ENVIRONMENT_欧式钟楼大道_可替换模型");
            BuildStreet(environment.transform);
            BuildArchitecture(environment.transform);
            BuildClocktower(environment.transform);
            BuildLighting(environment.transform);
            var props = BuildProps(environment.transform);

            var gameplay = New("GAMEPLAY_玩法与出生点");
            var spawnRoot = New("SPAWN_POINTS_敌人出生点", gameplay.transform);
            var spawnPoints = new Transform[5];
            for (var i = 0; i < spawnPoints.Length; i++)
            {
                var marker = New($"Spawn_{i + 1:00}_可调整", spawnRoot.transform);
                marker.transform.position = new Vector3((i - 2) * 2.55f, 0f, -3.5f - Mathf.Abs(i - 2) * .75f);
                marker.transform.rotation = Quaternion.LookRotation(Vector3.back);
                AddPrimitive($"标记_{i + 1}", PrimitiveType.Cylinder, marker.transform, Vector3.up * .04f, new Vector3(.32f, .025f, .32f), mats["spawn"], false);
                spawnPoints[i] = marker.transform;
            }
            var supply = New("SupplyPoint_补给投放点_可调整", gameplay.transform);
            supply.transform.position = new Vector3(0f, 1.15f, 5.4f);

            var rig = BuildRig();
            var managerGo = New("GravityGameManager_完整玩法", gameplay.transform);
            var manager = managerGo.AddComponent<GravityGameManager>();
            manager.playerHead = rig.camera.transform;
            manager.designStart = rig.root.GetComponent<GravityDesignStart>();
            manager.leftHand = rig.left;
            manager.rightHand = rig.right;
            manager.scoutRobotPrefab = prefabs.scout;
            manager.engineerRobotPrefab = prefabs.engineer;
            manager.guardRobotPrefab = prefabs.guard;
            manager.healthPackPrefab = prefabs.health;
            manager.spawnPoints = spawnPoints;
            manager.supplyPoint = supply.transform;
            manager.hud = BuildHud(rig.root.GetComponent<GravityDesignStart>());
            rig.shield.game = manager;
            managerGo.AddComponent<GravityAutomatedSmoke>().game = manager;

            var preview = New("SCENE_PREVIEW_ENEMIES_仅Scene查看", environment.transform);
            Preview(prefabs.scout, preview.transform, new Vector3(-4.6f, 0f, 1.8f), "预览_巡逻机");
            Preview(prefabs.guard, preview.transform, new Vector3(0f, 0f, -.3f), "预览_盾卫核心");
            Preview(prefabs.engineer, preview.transform, new Vector3(4.6f, 0f, 1.8f), "预览_工程机");

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            Selection.activeGameObject = environment;
        }

        private static void BuildStreet(Transform parent)
        {
            var street = New("STREET_道路与人行道", parent);
            AddPrimitive("Road_道路_18x120m", PrimitiveType.Cube, street.transform, new Vector3(0f, -.15f, -42f), new Vector3(18f, .3f, 120f), mats["road"]);
            for (var side = -1; side <= 1; side += 2)
            {
                AddPrimitive(side < 0 ? "Sidewalk_Left" : "Sidewalk_Right", PrimitiveType.Cube, street.transform, new Vector3(side * 11.8f, .04f, -42f), new Vector3(5.5f, .25f, 120f), mats["stone"]);
                for (var z = 8f; z > -98f; z -= 4f)
                    AddPrimitive($"Paving_{side}_{Mathf.Abs(z):000}", PrimitiveType.Cube, street.transform, new Vector3(side * 9.2f, .025f, z), new Vector3(.08f, .02f, 1.7f), mats["brass"], false);
            }
            for (var z = 8f; z > -98f; z -= 5f) AddPrimitive($"RoadMark_{Mathf.Abs(z):000}", PrimitiveType.Cube, street.transform, new Vector3(0f, .02f, z), new Vector3(.18f, .025f, 2.4f), mats["roadMark"], false);
        }

        private static void BuildArchitecture(Transform parent)
        {
            var architecture = New("ARCHITECTURE_两侧欧式建筑_逐栋可替换", parent);
            for (var side = -1; side <= 1; side += 2)
            {
                for (var index = 0; index < 10; index++)
                {
                    var z = 6f - index * 10.5f;
                    var building = New($"Building_{(side < 0 ? "L" : "R")}_{index + 1:00}_可替换", architecture.transform);
                    building.transform.position = new Vector3(side * (13.4f + (index % 2) * .35f), 0f, z);
                    building.transform.rotation = Quaternion.Euler(0f, side < 0 ? 90f : -90f, 0f);
                    var visual = New("REPLACE_VISUAL_Building_导入模型放这里", building.transform);
                    var height = 10f + (index % 3) * 1.2f;
                    AddPrimitive("StoneBody", PrimitiveType.Cube, visual.transform, new Vector3(0f, height * .5f, 0f), new Vector3(8.6f, height, 6.2f), index % 2 == 0 ? mats["stone"] : mats["stoneLight"]);
                    AddPrimitive("MansardRoof", PrimitiveType.Cube, visual.transform, new Vector3(0f, height + .65f, 0f), new Vector3(9.1f, 1.2f, 6.6f), mats["roof"]);
                    for (var floor = 0; floor < 4; floor++)
                    for (var column = -1; column <= 1; column++)
                        AddPrimitive($"WarmWindow_{floor}_{column}", PrimitiveType.Cube, visual.transform, new Vector3(column * 2.15f, 2.1f + floor * 2f, -3.13f), new Vector3(1.05f, 1.25f, .08f), mats["window"], false);
                }
            }
        }

        private static void BuildClocktower(Transform parent)
        {
            var tower = New("REPLACE_CLOCKTOWER_钟楼", parent);
            tower.transform.position = new Vector3(0f, 0f, -86f);
            var visual = New("REPLACE_VISUAL_Clocktower_导入模型放这里", tower.transform);
            AddPrimitive("TowerBase", PrimitiveType.Cube, visual.transform, new Vector3(0f, 7f, 0f), new Vector3(11f, 14f, 9f), mats["stoneLight"]);
            AddPrimitive("TowerUpper", PrimitiveType.Cube, visual.transform, new Vector3(0f, 17f, 0f), new Vector3(7.2f, 7f, 6.8f), mats["stone"]);
            AddPrimitive("Roof", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 23.2f, 0f), new Vector3(5.8f, 4f, 5.8f), mats["roof"], false, Quaternion.Euler(0f, 0f, 0f), 4);
            var clock = AddPrimitive("ClockFace", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 18.2f, 3.45f), new Vector3(2.45f, .08f, 2.45f), mats["clock"], false, Quaternion.Euler(90f, 0f, 0f));
            clock.transform.Rotate(Vector3.forward, 45f);
        }

        private static void BuildLighting(Transform parent)
        {
            var lights = New("LIGHTING_街灯与月光", parent);
            var moon = New("Moonlight", lights.transform).AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.color = new Color(.56f, .72f, 1f);
            moon.intensity = 1.35f;
            moon.transform.rotation = Quaternion.Euler(38f, -28f, 0f);
            for (var side = -1; side <= 1; side += 2)
            for (var index = 0; index < 9; index++)
            {
                var lamp = New($"REPLACE_LAMP_{(side < 0 ? "L" : "R")}_{index + 1:00}", lights.transform);
                lamp.transform.position = new Vector3(side * 9.1f, 0f, 5f - index * 11f);
                var visual = New("REPLACE_VISUAL_Lamp_导入模型放这里", lamp.transform);
                AddPrimitive("Pole", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 2.15f, 0f), new Vector3(.12f, 2.15f, .12f), mats["brassDark"], false);
                AddPrimitive("Lamp", PrimitiveType.Sphere, visual.transform, new Vector3(side * -.42f, 4.15f, 0f), Vector3.one * .34f, mats["warm"], false);
                if (index % 2 == 0)
                {
                    var light = New("WarmPointLight", lamp.transform).AddComponent<Light>();
                    light.type = LightType.Point;
                    light.color = Warm;
                    light.intensity = 4.5f;
                    light.range = 8f;
                    light.transform.localPosition = new Vector3(side * -.42f, 4.15f, 0f);
                }
            }
        }

        private static List<GravityTarget> BuildProps(Transform parent)
        {
            var root = New("PROPS_可吸取街道物", parent);
            var result = new List<GravityTarget>();
            for (var index = 0; index < 18; index++)
            {
                var side = index % 2 == 0 ? -1 : 1;
                var type = index % 3;
                var position = new Vector3(side * (7.4f + (index % 2) * .35f), .6f, 4f - index * 4.3f);
                result.Add(type == 0 ? CreateCrate(root.transform, index, position) : type == 1 ? CreateBench(root.transform, index, position) : CreateBin(root.transform, index, position));
            }
            for (var index = 0; index < 6; index++)
            {
                var side = index % 2 == 0 ? -1 : 1;
                var target = CreateEnergyCanister(root.transform, index, new Vector3(side * 6.5f, .75f, -12f - index * 10.5f));
                result.Add(target);
            }
            return result;
        }

        private static GravityTarget CreateCrate(Transform parent, int index, Vector3 position)
        {
            var root = TargetRoot($"Prop_Crate_{index + 1:00}_可替换", parent, position, new Vector3(.9f, .9f, .9f), GravityTargetKind.Prop, "Metal Crate", 1, 0, true);
            var visual = New("REPLACE_VISUAL_Crate_导入模型放这里", root.transform);
            AddPrimitive("CrateBody", PrimitiveType.Cube, visual.transform, Vector3.zero, new Vector3(.86f, .86f, .86f), mats["brassDark"], false);
            AddPrimitive("Band", PrimitiveType.Cube, visual.transform, Vector3.zero, new Vector3(.9f, .16f, .9f), mats["brass"], false);
            root.GetComponent<GravityTarget>().replaceableVisual = visual.transform;
            return root.GetComponent<GravityTarget>();
        }

        private static GravityTarget CreateBench(Transform parent, int index, Vector3 position)
        {
            var root = TargetRoot($"Prop_Bench_{index + 1:00}_可替换", parent, position, new Vector3(1.9f, 1.05f, .7f), GravityTargetKind.Prop, "Street Bench", 1, 0, true);
            var visual = New("REPLACE_VISUAL_Bench_导入模型放这里", root.transform);
            AddPrimitive("Seat", PrimitiveType.Cube, visual.transform, new Vector3(0f, -.12f, 0f), new Vector3(1.8f, .18f, .55f), mats["brassDark"], false);
            AddPrimitive("Back", PrimitiveType.Cube, visual.transform, new Vector3(0f, .35f, .24f), new Vector3(1.8f, .58f, .13f), mats["brass"], false);
            root.GetComponent<GravityTarget>().replaceableVisual = visual.transform;
            return root.GetComponent<GravityTarget>();
        }

        private static GravityTarget CreateBin(Transform parent, int index, Vector3 position)
        {
            var root = TargetRoot($"Prop_Bin_{index + 1:00}_可替换", parent, position, new Vector3(.75f, 1.05f, .75f), GravityTargetKind.Prop, "Street Bin", 1, 0, true);
            var visual = New("REPLACE_VISUAL_Bin_导入模型放这里", root.transform);
            AddPrimitive("BinBody", PrimitiveType.Cylinder, visual.transform, Vector3.zero, new Vector3(.48f, .52f, .48f), mats["brassDark"], false);
            AddPrimitive("Lid", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, .52f, 0f), new Vector3(.55f, .08f, .55f), mats["brass"], false);
            root.GetComponent<GravityTarget>().replaceableVisual = visual.transform;
            return root.GetComponent<GravityTarget>();
        }

        private static GravityTarget CreateEnergyCanister(Transform parent, int index, Vector3 position)
        {
            var root = TargetRoot($"Prop_EnergyCanister_{index + 1:00}_连锁爆炸", parent, position, new Vector3(.8f, 1.35f, .8f), GravityTargetKind.Prop, "Energy Canister", 1, 0, true);
            var visual = New("REPLACE_VISUAL_EnergyCanister_导入模型放这里", root.transform);
            AddPrimitive("Canister", PrimitiveType.Cylinder, visual.transform, Vector3.zero, new Vector3(.48f, .68f, .48f), mats["threat"], false);
            AddPrimitive("Core", PrimitiveType.Cylinder, visual.transform, Vector3.zero, new Vector3(.53f, .13f, .53f), mats["warm"], false);
            root.AddComponent<ChainReactionProp>();
            root.GetComponent<GravityTarget>().replaceableVisual = visual.transform;
            return root.GetComponent<GravityTarget>();
        }

        private static RigSet BuildRig()
        {
            var root = New("XR_RIG_PICO与桌面回退");
            var designStart = root.AddComponent<GravityDesignStart>();
            designStart.worldPosition = new Vector3(0f, 0f, 10.5f);
            designStart.worldYaw = 180f;
            designStart.eyeHeight = 2.336f;
            designStart.trackedViewLift = .2f;
            designStart.missionBoardOffset = new Vector3(-3.35f, 1.65f, 3.7f);
            designStart.ApplyToRig();
            var origin = root.AddComponent<XROrigin>();
            origin.RequestedTrackingOriginMode = XROrigin.TrackingOriginMode.Floor;
            origin.CameraYOffset = 0f;
            root.AddComponent<PXR_Manager>();
            var offset = New("Camera Offset", root.transform);
            origin.CameraFloorOffsetObject = offset;
            var cameraGo = New("Main Camera", offset.transform);
            cameraGo.tag = "MainCamera";
            cameraGo.transform.localPosition = new Vector3(0f, designStart.eyeHeight, 0f);
            cameraGo.transform.localRotation = Quaternion.identity;
            var camera = cameraGo.AddComponent<Camera>();
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = Night;
            camera.nearClipPlane = .05f;
            camera.farClipPlane = 160f;
            camera.fieldOfView = 67f;
            cameraGo.AddComponent<AudioListener>();
            var tracked = cameraGo.AddComponent<TrackedPoseDriver>();
            tracked.trackingType = TrackedPoseDriver.TrackingType.RotationAndPosition;
            tracked.updateType = TrackedPoseDriver.UpdateType.UpdateAndBeforeRender;
            tracked.positionInput = new InputActionProperty(new InputAction("Head Position", binding: "<XRHMD>/centerEyePosition"));
            tracked.rotationInput = new InputActionProperty(new InputAction("Head Rotation", binding: "<XRHMD>/centerEyeRotation"));
            origin.Camera = camera;
            var fallback = root.AddComponent<DesktopFallbackController>();
            fallback.rigRoot = root.transform;
            fallback.viewCamera = camera;
            fallback.designStart = designStart;

            var left = BuildHand("LeftHand_左手控制器", GravityHandSide.Left, offset.transform, camera);
            var right = BuildHand("RightHand_右手控制器", GravityHandSide.Right, offset.transform, camera);
            var shieldGo = New("PlayerShield_玩家受击区域", cameraGo.transform);
            var sphere = shieldGo.AddComponent<SphereCollider>();
            sphere.radius = .48f;
            sphere.isTrigger = true;
            var shield = shieldGo.AddComponent<GravityPlayerShield>();
            return new RigSet { root = root, camera = camera, left = left, right = right, shield = shield };
        }

        private static GravityHand BuildHand(string name, GravityHandSide side, Transform parent, Camera camera)
        {
            var root = New(name, parent);
            root.transform.localPosition = side == GravityHandSide.Left ? new Vector3(-.46f, 1.38f, -.68f) : new Vector3(.46f, 1.38f, -.68f);
            var visual = New(side == GravityHandSide.Left ? "VISUAL_原创机械重力护腕_左_紫" : "VISUAL_原创机械重力护腕_右_青", root.transform);
            visual.transform.localScale = Vector3.one * .68f;
            AddPrimitive("主金属护壳", PrimitiveType.Cube, visual.transform, new Vector3(0f, 0f, .035f), new Vector3(.18f, .105f, .27f), mats["brassDark"], false);
            AddPrimitive("上层装甲脊", PrimitiveType.Cube, visual.transform, new Vector3(0f, .067f, .045f), new Vector3(.125f, .035f, .2f), mats["stoneLight"], false);
            AddPrimitive("腕部黄铜环", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 0f, -.115f), new Vector3(.14f, .025f, .14f), mats["brass"], false, Quaternion.Euler(90f, 0f, 0f));
            AddPrimitive("前端黄铜护圈", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 0f, .205f), new Vector3(.13f, .025f, .13f), mats["brass"], false, Quaternion.Euler(90f, 0f, 0f));
            var glow = AddPrimitive("重力能量核心_射线起点", PrimitiveType.Sphere, visual.transform, new Vector3(0f, 0f, .285f), Vector3.one * .068f, side == GravityHandSide.Left ? mats["violet"] : mats["cyan"], false);
            var rayOrigin = New("GravityRayOrigin_能量发射口", glow.transform);
            rayOrigin.transform.localPosition = new Vector3(0f, 0f, .058f);
            var line = root.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.widthMultiplier = .012f;
            line.material = mats["ray"];
            line.numCapVertices = 4;
            var anchor = New("HeldObjectAnchor_手持物锚点", root.transform);
            anchor.transform.localPosition = new Vector3(0f, 0f, .72f);
            var hand = root.AddComponent<GravityHand>();
            hand.side = side;
            hand.playerCamera = camera;
            hand.holdAnchor = anchor.transform;
            hand.rayOrigin = rayOrigin.transform;
            hand.gravityRay = line;
            hand.handGlow = glow.GetComponent<Renderer>();
            return hand;
        }

        private static GravityHud BuildHud(GravityDesignStart designStart)
        {
            var canvasGo = New("COMBAT_HUD_相机前固定");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = Camera.main;
            canvas.sortingOrder = 20;
            canvas.planeDistance = 1.05f;
            var rect = canvasGo.GetComponent<RectTransform>();
            rect.sizeDelta = GravityHud.DesktopReferenceResolution;
            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = GravityHud.DesktopReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = .5f;
            canvasGo.AddComponent<GraphicRaycaster>();
            var hud = canvasGo.AddComponent<GravityHud>();
            hud.waveText = Label("Wave", canvasGo.transform, new Vector2(-760f, 405f), new Vector2(330f, 44f), 24, Color.white, TextAnchor.MiddleLeft);
            hud.enemyText = Label("Enemies", canvasGo.transform, new Vector2(-760f, 352f), new Vector2(330f, 38f), 21, new Color(.72f, .8f, .88f), TextAnchor.MiddleLeft);
            hud.leftText = Label("Left", canvasGo.transform, new Vector2(-760f, -275f), new Vector2(330f, 38f), 19, new Color(.78f, .55f, 1f), TextAnchor.MiddleLeft);
            hud.repulseText = Label("Repulse", canvasGo.transform, new Vector2(-760f, -329f), new Vector2(330f, 34f), 18, new Color(.82f, .55f, 1f), TextAnchor.MiddleLeft);
            hud.repulseFill = Bar("RepulseBar", canvasGo.transform, new Vector2(-760f, -359f), new Vector2(310f, 10f), mats["violet"].color);
            hud.scoreText = Label("Score", canvasGo.transform, new Vector2(760f, 405f), new Vector2(330f, 44f), 24, Cyan, TextAnchor.MiddleRight);
            hud.energyText = Label("Energy", canvasGo.transform, new Vector2(760f, 352f), new Vector2(330f, 38f), 21, new Color(.82f, .55f, 1f), TextAnchor.MiddleRight);
            hud.energyFill = Bar("EnergyBar", canvasGo.transform, new Vector2(760f, 323f), new Vector2(310f, 10f), mats["violet"].color);
            hud.shieldText = Label("Shield", canvasGo.transform, new Vector2(760f, -275f), new Vector2(330f, 34f), 18, Cyan, TextAnchor.MiddleRight);
            hud.shieldFill = Bar("ShieldBar", canvasGo.transform, new Vector2(760f, -305f), new Vector2(310f, 10f), mats["cyanUI"].color);
            hud.rightText = Label("Right", canvasGo.transform, new Vector2(760f, -365f), new Vector2(330f, 38f), 19, new Color(.78f, .55f, 1f), TextAnchor.MiddleRight);
            hud.statusText = Label("Status", canvasGo.transform, new Vector2(-760f, -425f), new Vector2(360f, 42f), 18, Color.white, TextAnchor.LowerLeft);
            hud.statusText.gameObject.SetActive(false);
            var reticle = Label("Reticle", canvasGo.transform, Vector2.zero, new Vector2(50f, 50f), 34, new Color(.78f, .55f, 1f), TextAnchor.MiddleCenter);
            reticle.text = "+";
            reticle.gameObject.SetActive(false);
            var help = Label("Help", canvasGo.transform, new Vector2(0f, -390f), new Vector2(1240f, 34f), 16, new Color(.55f, .66f, .75f), TextAnchor.MiddleCenter);
            help.text = "[Trigger/Grip] TO GRAB · RELEASE TO THROW · CLAP HANDS TO COLLIDE · [Left X] REPULSE · [Right A] SINGULARITY · [Left Y] RECENTER  |  Desktop: Q/E · Space · F · R · C";
            help.gameObject.SetActive(false);

            hud.defeatPanel = New("DefeatPanel", canvasGo.transform);
            var panelRect = hud.defeatPanel.AddComponent<RectTransform>();
            panelRect.sizeDelta = new Vector2(760f, 330f);
            panelRect.anchoredPosition = Vector2.zero;
            var panelImage = hud.defeatPanel.AddComponent<Image>();
            panelImage.color = new Color(.025f, .035f, .065f, .94f);
            hud.defeatText = Label("DefeatText", hud.defeatPanel.transform, Vector2.zero, new Vector2(700f, 280f), 34, Color.white, TextAnchor.MiddleCenter);
            hud.defeatPanel.SetActive(false);
            return hud;
        }

        private static Text Label(string name, Transform parent, Vector2 position, Vector2 size, int fontSize, Color color, TextAnchor anchor)
        {
            var go = New(name, parent);
            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            var text = go.AddComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private static Image Bar(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var background = New(name + "_Track", parent);
            var bgRect = background.AddComponent<RectTransform>();
            bgRect.sizeDelta = size;
            bgRect.anchoredPosition = position;
            background.AddComponent<Image>().color = new Color(.2f, .28f, .38f, .6f);
            var fillGo = New(name + "_Fill", background.transform);
            var rect = fillGo.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var fill = fillGo.AddComponent<Image>();
            fill.color = color;
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = 0;
            return fill;
        }

        private static PrefabSet CreatePrefabs()
        {
            var projectile = SavePrefab(CreateProjectile(), Root + "/Prefabs/EnemyProjectile.prefab");
            var scout = SavePrefab(CreateRobot("Robot_Scout_巡逻机", "Scout", 1, 14, mats["robot"], mats["cyan"], projectile, 1.08f, 2.7f), Root + "/Prefabs/Robot_Scout.prefab");
            var engineer = SavePrefab(CreateRobot("Robot_Engineer_工程机", "Engineer", 1, 14, mats["robotWarm"], mats["cyan"], projectile, .92f, 2.45f), Root + "/Prefabs/Robot_Engineer.prefab");
            var guard = SavePrefab(CreateRobot("Robot_Guard_盾卫核心", "Shield Core", 2, 22, mats["robotDark"], mats["threat"], projectile, .72f, 1.95f), Root + "/Prefabs/Robot_Guard.prefab");
            var health = SavePrefab(CreateHealthPack(), Root + "/Prefabs/HealthPack.prefab");
            return new PrefabSet { projectile = projectile, scout = scout, engineer = engineer, guard = guard, health = health };
        }

        private static GameObject CreateRobot(string name, string displayName, int health, int charge, Material body, Material core, GameObject projectile, float speed, float interval)
        {
            var root = New(name);
            root.AddComponent<CapsuleCollider>().height = 2.45f;
            root.GetComponent<CapsuleCollider>().radius = .62f;
            root.GetComponent<CapsuleCollider>().center = new Vector3(0f, 1.2f, 0f);
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.mass = health > 1 ? 3.4f : 2.2f;
            rigidbody.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var target = root.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Robot;
            target.displayName = displayName;
            target.maxHealth = health;
            target.ultimateCharge = charge;
            target.canBlockOnce = true;
            var visual = New("REPLACE_VISUAL_Robot_导入分件模型放这里", root.transform);
            target.replaceableVisual = visual.transform;
            AddPrimitive("Torso", PrimitiveType.Cylinder, visual.transform, new Vector3(0f, 1.4f, 0f), new Vector3(.55f, .55f, .55f), body, false, Quaternion.identity, 8);
            AddPrimitive("Head", PrimitiveType.Cube, visual.transform, new Vector3(0f, 2.15f, 0f), new Vector3(.55f, .45f, .5f), mats["robotDark"], false);
            AddPrimitive("ChestCore", PrimitiveType.Sphere, visual.transform, new Vector3(0f, 1.48f, -.53f), Vector3.one * .18f, core, false);
            AddPrimitive("Visor", PrimitiveType.Cube, visual.transform, new Vector3(0f, 2.18f, -.27f), new Vector3(.36f, .07f, .04f), core, false);
            for (var side = -1; side <= 1; side += 2)
            {
                AddPrimitive($"Shoulder_{side}", PrimitiveType.Sphere, visual.transform, new Vector3(side * .62f, 1.72f, 0f), Vector3.one * .24f, body, false);
                AddPrimitive($"Arm_{side}", PrimitiveType.Cube, visual.transform, new Vector3(side * .72f, 1.32f, 0f), new Vector3(.2f, .7f, .22f), body, false);
                AddPrimitive($"Leg_{side}", PrimitiveType.Cube, visual.transform, new Vector3(side * .27f, .48f, 0f), new Vector3(.25f, .75f, .28f), body, false);
                AddPrimitive($"Foot_{side}", PrimitiveType.Cube, visual.transform, new Vector3(side * .27f, .1f, -.1f), new Vector3(.31f, .18f, .48f), mats["robotDark"], false);
            }
            var muzzle = New("ProjectileMuzzle", root.transform);
            muzzle.transform.localPosition = new Vector3(0f, 1.5f, -.72f);
            var enemy = root.AddComponent<RobotEnemy>();
            enemy.moveSpeed = speed;
            enemy.attackInterval = interval;
            enemy.projectilePrefab = projectile;
            enemy.muzzle = muzzle.transform;
            return root;
        }

        private static GameObject CreateProjectile()
        {
            var root = New("Projectile_危险能量弹");
            var sphere = root.AddComponent<SphereCollider>();
            sphere.radius = .24f;
            sphere.isTrigger = true;
            var rigidbody = root.AddComponent<Rigidbody>();
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            var target = root.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Projectile;
            target.displayName = "Danger Projectile";
            target.maxHealth = 1;
            target.canBlockOnce = false;
            var visual = New("REPLACE_VISUAL_Projectile", root.transform);
            AddPrimitive("Core", PrimitiveType.Sphere, visual.transform, Vector3.zero, Vector3.one * .28f, mats["threat"], false);
            target.replaceableVisual = visual.transform;
            root.AddComponent<EnemyProjectile>();
            return root;
        }

        private static GameObject CreateHealthPack()
        {
            var root = New("HealthPack_防卫补给");
            root.AddComponent<BoxCollider>().size = Vector3.one * .75f;
            var body = root.AddComponent<Rigidbody>();
            body.useGravity = false;
            var target = root.AddComponent<GravityTarget>();
            target.kind = GravityTargetKind.Heal;
            target.displayName = "Shield Supply";
            target.canBlockOnce = false;
            var visual = New("REPLACE_VISUAL_HealthPack", root.transform);
            AddPrimitive("Pack", PrimitiveType.Cube, visual.transform, Vector3.zero, Vector3.one * .7f, mats["heal"], false);
            AddPrimitive("CrossV", PrimitiveType.Cube, visual.transform, new Vector3(0f, 0f, -.37f), new Vector3(.15f, .5f, .04f), mats["cyan"], false);
            AddPrimitive("CrossH", PrimitiveType.Cube, visual.transform, new Vector3(0f, 0f, -.39f), new Vector3(.5f, .15f, .04f), mats["cyan"], false);
            target.replaceableVisual = visual.transform;
            return root;
        }

        private static GameObject TargetRoot(string name, Transform parent, Vector3 position, Vector3 colliderSize, GravityTargetKind kind, string display, int health, int charge, bool canBlock)
        {
            var root = New(name, parent);
            root.transform.position = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            var body = root.AddComponent<Rigidbody>();
            body.mass = Mathf.Max(1f, colliderSize.magnitude);
            body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
            var target = root.AddComponent<GravityTarget>();
            target.kind = kind;
            target.displayName = display;
            target.maxHealth = health;
            target.ultimateCharge = charge;
            target.canBlockOnce = canBlock;
            return root;
        }

        private static void Preview(GameObject prefab, Transform parent, Vector3 position, string name)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            instance.name = name;
            instance.transform.position = position;
            instance.transform.rotation = Quaternion.LookRotation(new Vector3(0f, 0f, 10.5f) - position, Vector3.up);
        }

        private static GameObject SavePrefab(GameObject source, string path)
        {
            var prefab = PrefabUtility.SaveAsPrefabAsset(source, path);
            UnityEngine.Object.DestroyImmediate(source);
            return prefab;
        }

        private static void ConfigureProject()
        {
            PlayerSettings.companyName = "Gravity Warden Studio";
            PlayerSettings.productName = "GRAVITY FRONTLINE";
            PlayerSettings.bundleVersion = "0.1.0";
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.gravitywarden.repulsion");
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel29;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            PlayerSettings.Android.applicationEntry = AndroidApplicationEntry.Activity;
            PlayerSettings.Android.useCustomKeystore = false;
            PlayerSettings.colorSpace = ColorSpace.Linear;
            PlayerSettings.MTRendering = true;
            PlayerSettings.gpuSkinning = true;
            PlayerSettings.runInBackground = false;
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { GraphicsDeviceType.OpenGLES3, GraphicsDeviceType.Vulkan });
            PlayerSettings.SetMobileMTRendering(NamedBuildTarget.Android, true);
            QualitySettings.vSyncCount = 0;
            QualitySettings.antiAliasing = 2;
            QualitySettings.pixelLightCount = 4;
            QualitySettings.anisotropicFiltering = AnisotropicFiltering.Enable;
            Physics.defaultSolverIterations = 8;
            Physics.defaultContactOffset = .01f;
            SetInputHandler(1);
        }

        private static void ConfigurePico()
        {
            var store = LoadOrCreateXrSettingsStore();
            if (!store.HasSettingsForBuildTarget(BuildTargetGroup.Android)) store.CreateDefaultSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!store.HasManagerSettingsForBuildTarget(BuildTargetGroup.Android)) store.CreateDefaultManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            var manager = store.ManagerSettingsForBuildTarget(BuildTargetGroup.Android);
            if (!XRPackageMetadataStore.AssignLoader(manager, LoaderType, BuildTargetGroup.Android) &&
                !manager.activeLoaders.Any(loader => loader != null && loader.GetType().FullName == LoaderType))
                throw new InvalidOperationException("Could not assign official PICO PXR_Loader");
            var general = store.SettingsForBuildTarget(BuildTargetGroup.Android);
            general.InitManagerOnStart = true;
            var settings = LoadPicoSettingsAsset();
            settings.appMode = PXR_Settings.AppMode.XR;
            settings.stereoRenderingModeAndroid = PXR_Settings.StereoRenderingModeAndroid.Multiview;
            settings.systemDisplayFrequency = PXR_Settings.SystemDisplayFrequency.Default;
            settings.optimizeBufferDiscards = true;
            EditorBuildSettings.AddConfigObject(PicoSettingsKey, settings, true);
            EditorBuildSettings.AddConfigObject(XRGeneralSettings.k_SettingsKey, store, true);
            EditorUtility.SetDirty(settings);
            EditorUtility.SetDirty(store);
            EditorUtility.SetDirty(manager);
            EditorUtility.SetDirty(general);
        }

        private static XRGeneralSettingsPerBuildTarget LoadOrCreateXrSettingsStore()
        {
            var existing = AssetDatabase.FindAssets("t:XRGeneralSettingsPerBuildTarget")
                .Select(g => AssetDatabase.LoadAssetAtPath<XRGeneralSettingsPerBuildTarget>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x != null);
            if (existing != null) return existing;
            var store = ScriptableObject.CreateInstance<XRGeneralSettingsPerBuildTarget>();
            AssetDatabase.CreateAsset(store, "Assets/XR/Settings/XRGeneralSettingsPerBuildTarget.asset");
            return store;
        }

        private static PXR_Settings LoadPicoSettingsAsset()
        {
            var settings = PXR_Settings.GetSettings();
            if (settings != null) return settings;
            var existing = AssetDatabase.FindAssets("t:PXR_Settings")
                .Select(g => AssetDatabase.LoadAssetAtPath<PXR_Settings>(AssetDatabase.GUIDToAssetPath(g))).FirstOrDefault(x => x != null);
            if (existing != null) return existing;
            settings = ScriptableObject.CreateInstance<PXR_Settings>();
            AssetDatabase.CreateAsset(settings, "Assets/XR/Settings/PXR_Settings.asset");
            return settings;
        }

        private static Dictionary<string, Material> CreateMaterials()
        {
            return new Dictionary<string, Material>
            {
                ["road"] = Material("M_Road", new Color(.035f, .065f, .1f), 0f, .78f),
                ["roadMark"] = Material("M_RoadMark", new Color(.5f, .47f, .35f), .35f, .55f),
                ["stone"] = Material("M_Stone", Stone, .04f, .82f),
                ["stoneLight"] = Material("M_StoneLight", StoneLight, .05f, .76f),
                ["roof"] = Material("M_Roof", new Color(.035f, .065f, .1f), .3f, .55f),
                ["brass"] = Material("M_Brass", Brass, .72f, .38f),
                ["brassDark"] = Material("M_BrassDark", new Color(.13f, .14f, .13f), .78f, .42f),
                ["window"] = Material("M_WindowWarm", new Color(.62f, .28f, .09f), .1f, .32f, Warm, 2.7f),
                ["warm"] = Material("M_WarmLight", Warm, .05f, .25f, Warm, 4f),
                ["clock"] = Material("M_Clock", new Color(.72f, .65f, .42f), .55f, .24f, new Color(1f, .72f, .3f), 2.2f),
                ["robot"] = Material("M_Robot", new Color(.28f, .39f, .47f), .82f, .3f),
                ["robotWarm"] = Material("M_RobotWarm", new Color(.38f, .24f, .18f), .75f, .34f),
                ["robotDark"] = Material("M_RobotDark", new Color(.06f, .13f, .18f), .9f, .28f),
                ["cyan"] = Material("M_CyanCore", Cyan, .3f, .22f, Cyan, 5f),
                ["cyanUI"] = Material("M_CyanUI", Cyan, 0f, .2f),
                ["violet"] = Material("M_GravityViolet", Violet, .2f, .22f, Violet, 5f),
                ["threat"] = Material("M_ThreatRed", Threat, .25f, .25f, Threat, 5f),
                ["heal"] = Material("M_Heal", new Color(.25f, 1f, .64f), .2f, .28f, new Color(.18f, 1f, .56f), 4f),
                ["spawn"] = Material("M_Spawn", Violet, .1f, .25f, Violet, 3f),
                ["ray"] = Material("M_GravityRay", Violet, .05f, .25f, Violet, 4f)
            };
        }

        private static Material Material(string name, Color color, float metallic, float smoothness, Color? emission = null, float emissionPower = 0f)
        {
            var path = Root + "/Materials/" + name + ".mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            var material = existing != null ? existing : new Material(Shader.Find("Standard"));
            material.name = name;
            material.color = color;
            material.SetFloat("_Metallic", metallic);
            material.SetFloat("_Glossiness", smoothness);
            if (emission.HasValue)
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission.Value * emissionPower);
            }
            if (existing == null) AssetDatabase.CreateAsset(material, path);
            else EditorUtility.SetDirty(material);
            return material;
        }

        private static GameObject AddPrimitive(string name, PrimitiveType type, Transform parent, Vector3 localPosition, Vector3 localScale, Material material, bool collider = true, Quaternion? localRotation = null, int cylinderSides = 0)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation ?? Quaternion.identity;
            go.transform.localScale = localScale;
            go.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        private static GameObject New(string name, Transform parent = null)
        {
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            return go;
        }

        private static void EnsureFolders()
        {
            string[] folders = { Root, Root + "/Scenes", Root + "/Prefabs", Root + "/Materials", Root + "/Documentation", "Assets/XR", "Assets/XR/Settings" };
            foreach (var folder in folders) if (!AssetDatabase.IsValidFolder(folder))
            {
                var parent = folder[..folder.LastIndexOf('/')];
                var name = folder[(folder.LastIndexOf('/') + 1)..];
                AssetDatabase.CreateFolder(parent, name);
            }
        }

        private static void SetInputHandler(int value)
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0) throw new InvalidOperationException("Could not open ProjectSettings.asset");
            var serialized = new SerializedObject(assets[0]);
            var property = serialized.FindProperty("activeInputHandler") ?? throw new InvalidOperationException("activeInputHandler setting not found");
            property.intValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static int ReadInputHandler()
        {
            var assets = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (assets.Length == 0) return -1;
            return new SerializedObject(assets[0]).FindProperty("activeInputHandler")?.intValue ?? -1;
        }

        private static void Build(BuildTarget target, string output, BuildOptions options)
        {
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath }, locationPathName = output, target = target, options = options
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            Debug.Log($"GRAVITY_BUILD_PASS target={target} output={output} size={report.summary.totalSize}");
        }

        private sealed class PrefabSet
        {
            public GameObject projectile;
            public GameObject scout;
            public GameObject engineer;
            public GameObject guard;
            public GameObject health;
        }

        private sealed class RigSet
        {
            public GameObject root;
            public Camera camera;
            public GravityHand left;
            public GravityHand right;
            public GravityPlayerShield shield;
        }
    }
}
