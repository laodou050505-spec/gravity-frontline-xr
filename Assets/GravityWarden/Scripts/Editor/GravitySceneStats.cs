using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravityRepulsion.Editor
{
    /// <summary>
    /// 统计活动场景中 GameObject、MeshRenderer、Collider、Rigidbody、MonoBehaviour 数量。
    /// -executeMethod GravityRepulsion.Editor.GravitySceneStats.PrintStats
    /// </summary>
    public static class GravitySceneStats
    {
        public static void PrintStats()
        {
            const string ScenePath = "Assets/GravityWarden/Scenes/GravityBoulevard.unity";
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var allGO   = Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mr      = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var box     = Object.FindObjectsByType<BoxCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var sph     = Object.FindObjectsByType<SphereCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mesh    = Object.FindObjectsByType<MeshCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var caps    = Object.FindObjectsByType<CapsuleCollider>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var rb      = Object.FindObjectsByType<Rigidbody>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var mb      = Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            // 统计投射阴影的 MeshRenderer 数量
            int shadowCasters = 0;
            foreach (var r in mr)
                if (r.shadowCastingMode != UnityEngine.Rendering.ShadowCastingMode.Off)
                    shadowCasters++;

            Debug.Log($"[SceneStats] GO={allGO.Length} Box={box.Length} Mesh={mesh.Length} Sph={sph.Length} Caps={caps.Length} RB={rb.Length} MB={mb.Length} MR={mr.Length} ShadowCasters={shadowCasters}");

            EditorApplication.Exit(0);
        }
    }
}
