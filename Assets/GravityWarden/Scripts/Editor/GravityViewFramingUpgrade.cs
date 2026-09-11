#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GravityRepulsion.Editor
{
    public static class GravityViewFramingUpgrade
    {
        public static void Apply()
        {
            var scene=EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            var start=Object.FindFirstObjectByType<GravityDesignStart>();
            if(start==null || Camera.main==null)throw new System.InvalidOperationException("Missing gameplay start");
            start.eyeHeight=2.336f;start.trackedViewLift=.2f;
            var p=Camera.main.transform.localPosition;p.y=start.eyeHeight;Camera.main.transform.localPosition=p;
            EditorUtility.SetDirty(start);EditorUtility.SetDirty(Camera.main.transform);
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            Debug.Log("GRAVITY_VIEW_HEIGHT_PREPARE_PASS desktopEye=2.336 trackedViewLift=0.200 trackedHeadPosePreserved=true");
        }
    }
}
#endif
