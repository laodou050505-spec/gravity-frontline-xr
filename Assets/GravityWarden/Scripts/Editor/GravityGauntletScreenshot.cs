using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace GravityRepulsion.Editor
{
    /// <summary>
    /// 在一个临时场景中实例化护腕模型，从第一人称握持视角渲染并保存截图。
    /// 通过 -executeMethod GravityRepulsion.Editor.GravityGauntletScreenshot.CaptureHandView 调用。
    /// </summary>
    public static class GravityGauntletScreenshot
    {
        private const string FbxPath    = "Assets/GravityWarden/Art/GravityGauntlet_R.fbx";
        private const string OutputPath = "Preview/GauntletV4_UnityHandView.png";
        private const int    Width      = 960;
        private const int    Height     = 720;

        public static void CaptureHandView()
        {
            Debug.Log("[GauntletShot] 开始截图流程...");

            // ── 新建临时场景 ──
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ── 加载 FBX 模型 ──
            var modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(FbxPath);
            if (modelPrefab == null)
            {
                Debug.LogError($"[GauntletShot] 找不到 FBX: {FbxPath}");
                EditorApplication.Exit(1);
                return;
            }

            // 实例化模型
            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            // 把护腕放在原点，模拟戴在右手腕上的位置
            // 坐标系：X 右，Y 上，Z 向屏幕内
            model.transform.position = new Vector3(0.10f, -0.08f, 0.38f);
            model.transform.eulerAngles = new Vector3(15f, -25f, -8f);
            model.transform.localScale = Vector3.one;

            // ── 添加光源（模拟室外/战场环境）──
            var sunGo = new GameObject("Sun");
            var sunLight = sunGo.AddComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.intensity = 1.2f;
            sunLight.color = new Color(0.95f, 0.92f, 0.85f);
            sunGo.transform.eulerAngles = new Vector3(45f, -30f, 0f);

            var fillGo = new GameObject("Fill");
            var fillLight = fillGo.AddComponent<Light>();
            fillLight.type = LightType.Directional;
            fillLight.intensity = 0.25f;
            fillLight.color = new Color(0.4f, 0.55f, 0.9f);
            fillGo.transform.eulerAngles = new Vector3(20f, 150f, 0f);

            // ── 设置摄像机（第一人称握持视角）──
            var camGo = new GameObject("CaptureCam");
            var cam = camGo.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.04f, 0.06f, 0.10f, 1f);
            cam.fieldOfView = 55f;
            // 摄像机在原点，LookAt 护腕中心
            camGo.transform.position = Vector3.zero;
            camGo.transform.LookAt(model.transform.position);

            // 环境光（深蓝黑）
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.05f, 0.08f);

            // ── 渲染到 RenderTexture ──
            var rt = new RenderTexture(Width, Height, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            // 读取像素并保存
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            var png = tex.EncodeToPNG();
            var projectRoot = Path.GetDirectoryName(Application.dataPath);
            var savePath = Path.Combine(projectRoot, OutputPath);
            Directory.CreateDirectory(Path.GetDirectoryName(savePath));
            File.WriteAllBytes(savePath, png);

            var kb = png.Length / 1024;
            Debug.Log($"[GauntletShot] 截图已保存 => {savePath}  {kb}KB");

            // 清理
            cam.targetTexture = null;
            rt.Release();
            Object.DestroyImmediate(tex);

            EditorApplication.Exit(0);
        }
    }
}
