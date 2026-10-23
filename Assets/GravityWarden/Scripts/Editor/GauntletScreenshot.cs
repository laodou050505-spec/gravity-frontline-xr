using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using System.IO;

namespace GravityRepulsion.Editor
{
    public static class GauntletScreenshot
    {
        [MenuItem("Gravity/Capture Gauntlet Screenshots")]
        public static void CaptureScreenshots()
        {
            string scenePath = "Assets/GravityWarden/Scenes/GravityBoulevard.unity";
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            
            string outDir = "/tmp/gravity_p2/screenshots";
            Directory.CreateDirectory(outDir);

            // Find left and right hand GameObjects
            var visualRight = GameObject.Find("VISUAL_原创机械重力护腕_右_青");
            var visualLeft  = GameObject.Find("VISUAL_原创机械重力护腕_左_紫");

            if (visualRight == null || visualLeft == null)
            {
                Debug.LogError($"找不到手柄对象: right={visualRight}, left={visualLeft}");
                return;
            }

            // Create render texture
            int width = 1280, height = 720;
            var rt = new RenderTexture(width, height, 24, RenderTextureFormat.ARGB32);
            rt.antiAliasing = 4;

            // Create temp camera
            var camObj = new GameObject("_TempScreenshotCam");
            var cam = camObj.AddComponent<Camera>();
            cam.clearFlags = CameraClearFlags.Skybox;
            cam.targetTexture = rt;
            cam.fieldOfView = 60f;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 100f;

            // === RIGHT HAND CLOSE-UP ===
            var rb = GetObjectBounds(visualRight);
            var rCenter = rb.center;
            cam.transform.position = rCenter + new Vector3(0.05f, 0.02f, -0.3f);
            cam.transform.LookAt(rCenter);
            RenderAndSave(cam, rt, $"{outDir}/hand_right.png", width, height);

            // === LEFT HAND CLOSE-UP ===
            var lb = GetObjectBounds(visualLeft);
            var lCenter = lb.center;
            cam.transform.position = lCenter + new Vector3(-0.05f, 0.02f, -0.3f);
            cam.transform.LookAt(lCenter);
            RenderAndSave(cam, rt, $"{outDir}/hand_left.png", width, height);

            // === BOTH HANDS TOGETHER ===
            Vector3 midpoint = (rCenter + lCenter) * 0.5f;
            float handSpan = Vector3.Distance(rCenter, lCenter) + 0.3f;
            cam.transform.position = midpoint + new Vector3(0f, 0.1f, -handSpan);
            cam.transform.LookAt(midpoint);
            RenderAndSave(cam, rt, $"{outDir}/hands_both.png", width, height);

            // Cleanup
            Object.DestroyImmediate(camObj);
            rt.Release();
            Object.DestroyImmediate(rt);

            Debug.Log($"[GauntletScreenshot] Saved to {outDir}");
        }

        static Bounds GetObjectBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0) return new Bounds(go.transform.position, Vector3.one * 0.1f);
            var b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        static void RenderAndSave(Camera cam, RenderTexture rt, string path, int w, int h)
        {
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            Debug.Log($"[GauntletScreenshot] Saved: {path}");
        }
    }
}
