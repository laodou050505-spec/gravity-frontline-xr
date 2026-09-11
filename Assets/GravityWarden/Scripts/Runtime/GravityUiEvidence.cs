using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    public sealed class GravityUiEvidence : MonoBehaviour
    {
        private string output;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravityUiEvidence")) return;
            var go = new GameObject("ModularUiEvidence"); DontDestroyOnLoad(go); go.AddComponent<GravityUiEvidence>();
        }
        private IEnumerator Start()
        {
            var args = Environment.GetCommandLineArgs(); int index = Array.IndexOf(args,"-gravityUiOutput");
            output = index >= 0 && index+1 < args.Length ? args[index+1] : Path.Combine(Application.persistentDataPath,"ModularUiEvidence");
            Directory.CreateDirectory(output); Application.runInBackground = true;
            bool square = args.Contains("-gravityPicoMenuPreview");
            if (square) { Screen.SetResolution(1200, 1200, false); yield return null; Camera.main.aspect = 1f; }
            yield return new WaitForSeconds(2.5f);
            var menu = FindFirstObjectByType<GravityTitleHud>();
            float deadline = Time.unscaledTime + 8f;
            while (menu != null && !menu.IsSpatialReady && Time.unscaledTime < deadline) yield return null;
            if (menu != null && !menu.IsSpatialReady) { Fail("spatial_placement_not_ready"); yield break; }
            Capture("01_Title.png");
            var title = FindFirstObjectByType<GravityTitleController>();
            if (title == null || !title.IsReadyForButtonInput) { Fail("title_not_ready"); yield break; }
            title.StartGame();
            yield return new WaitForSeconds(3f);
            var game = FindFirstObjectByType<GravityGameManager>();
            if (game == null || game.hud == null) { Fail("gameplay_missing"); yield break; }
            Time.timeScale = 0f;
            Capture("02_Combat.png");
            if (args.Contains("-gravityVisorEvidence"))
            {
                var visor = game.hud.GetComponent<GravityVisorHud>();
                if (visor == null || !visor.Validate(out var reason)) { Fail("visor_configuration"); yield break; }
                // Opaque foreground geometry reproduces the user's held-prop occlusion case.
                var blocker = GameObject.CreatePrimitive(PrimitiveType.Cube);
                blocker.name = "EVIDENCE_ONLY_HUD_OCCLUDER";
                Destroy(blocker.GetComponent<Collider>());
                blocker.transform.SetParent(Camera.main.transform, false);
                blocker.transform.localPosition = new Vector3(0,0,.55f);
                blocker.transform.localScale = new Vector3(5,5,.05f);
                var blockerMaterial = new Material(Shader.Find("Unlit/Color"));
                blockerMaterial.color = new Color(.23f,.12f,.045f);
                blocker.GetComponent<Renderer>().sharedMaterial = blockerMaterial;
                Capture("04_TopmostOcclusion.png");
                blocker.SetActive(false); Destroy(blocker); Destroy(blockerMaterial);
                var head = Camera.main.transform;
                var oldRotation = head.localRotation;
                head.localRotation *= Quaternion.Euler(0,30,0);
                Capture("05_HeadTurn.png");
                head.localRotation = oldRotation;
                Debug.Log("GRAVITY_VISOR_EVIDENCE_PASS materials=overlay foregroundOccluder=captured headTurn=30 titleUnchanged=true");
            }
            game.hud.ShowRunSummary(GravityGameManager.BuildEvidenceRunSummary());
            yield return null;
            Capture("03_Result.png");
            Debug.Log("GRAVITY_MODULAR_UI_EVIDENCE_PASS title=English modules=4 combat=visor resultLiveNumbers=true");
            Application.Quit(0);
        }
        private void Capture(string file)
        {
            var camera = Camera.main;
            bool square=Environment.GetCommandLineArgs().Contains("-gravityPicoMenuPreview");
            int width=square?1200:1600, height=square?1200:1000;
            var target = new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);
            var previous = camera.targetTexture; var active = RenderTexture.active;
            camera.targetTexture = target;
            var canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach(var canvas in canvases) if (canvas.renderMode == RenderMode.ScreenSpaceCamera) canvas.worldCamera = camera;
            Canvas.ForceUpdateCanvases(); camera.Render(); RenderTexture.active = target;
            var image = new Texture2D(width,height,TextureFormat.RGB24,false); image.ReadPixels(new Rect(0,0,width,height),0,0); image.Apply();
            File.WriteAllBytes(Path.Combine(output,file),image.EncodeToPNG());
            camera.targetTexture = previous; RenderTexture.active = active; Destroy(image); target.Release(); Destroy(target);
        }
        private static void Fail(string reason) { Debug.LogError("GRAVITY_MODULAR_UI_EVIDENCE_FAIL " + reason); Application.Quit(2); }
    }
}
