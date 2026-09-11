#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravityRepulsion.Editor
{
    public static class GravityArchitectureUpgrade
    {
        private static MeshRenderer[] Models() => UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponentInParent<GravityTarget>() == null
                && r.name.StartsWith("tripo_convert_", StringComparison.Ordinal)
                && r.bounds.size.y > 3f && r.bounds.size.x > 1f && r.bounds.size.z > 1f)
            .OrderBy(r => r.name).ToArray();

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            var models = Models();
            if (models.Length < 30) throw new InvalidOperationException("Imported architecture inventory unexpectedly incomplete");
            int added = 0;
            foreach (var renderer in models)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null)
                    throw new InvalidOperationException("Missing imported architecture mesh: " + renderer.name);
                var marker = renderer.GetComponent<GravityArchitectureSolid>();
                if (marker == null) { marker = renderer.gameObject.AddComponent<GravityArchitectureSolid>(); added++; }
                var collider = renderer.GetComponent<BoxCollider>();
                // Mesh bounds are local: import rotations must be applied only once by the transform.
                collider.center = filter.sharedMesh.bounds.center;
                collider.size = filter.sharedMesh.bounds.size;
                collider.enabled = true;
                collider.isTrigger = false;
                collider.contactOffset = .005f;
                marker.isBuilding = renderer.bounds.size.y > 6f
                    && Mathf.Min(renderer.bounds.size.x, renderer.bounds.size.z) > 3f;
                renderer.gameObject.isStatic = true;
                EditorUtility.SetDirty(collider);
                EditorUtility.SetDirty(marker);
                Debug.Log($"ARCHITECTURE_SOLID name={renderer.name} building={marker.isBuilding} centre={renderer.bounds.center:F3} size={renderer.bounds.size:F3}");
            }
            Physics.SyncTransforms();
            var props = UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(p => p.kind == GravityTargetKind.Prop).ToArray();
            int moved = 0;
            foreach (var prop in props)
            {
                prop.RefreshGeometryCache();
                var previous = prop.transform.position;
                if (GravityArchitectureCollision.ResolvePenetration(prop))
                {
                    moved++;
                    Debug.Log($"ARCHITECTURE_INITIAL_PLACEMENT name={prop.name} movement={(prop.transform.position-previous):F4} distance={Vector3.Distance(previous,prop.transform.position):F4}");
                    EditorUtility.SetDirty(prop.transform);
                    PrefabUtility.RecordPrefabInstancePropertyModifications(prop.transform);
                }
                if (GravityArchitectureCollision.HasPenetration(prop, .003f))
                    throw new InvalidOperationException("Cannot find clear initial placement: " + prop.name);
            }
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();
            Debug.Log($"GRAVITY_ARCHITECTURE_UPGRADE_PASS models={models.Length} buildings={models.Count(r=>r.GetComponent<GravityArchitectureSolid>().isBuilding)} fixtures={models.Count(r=>!r.GetComponent<GravityArchitectureSolid>().isBuilding)} added={added} movedInitialProps={moved} props={props.Length} independentShapes=true");
            Audit();
        }

        public static void Audit()
        {
            EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            Physics.SyncTransforms();
            var models = Models();
            foreach (var renderer in models)
            {
                var marker = renderer.GetComponent<GravityArchitectureSolid>();
                var box = renderer.GetComponent<BoxCollider>();
                var local = renderer.GetComponent<MeshFilter>().sharedMesh.bounds;
                if (marker == null || box == null || !box.enabled || box.isTrigger || box.attachedRigidbody != null
                    || Vector3.Distance(box.center,local.center)>.001f || Vector3.Distance(box.size,local.size)>.001f)
                    throw new InvalidOperationException("Architecture collision mismatch: " + renderer.name);
            }
            var props = UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(p => p.kind == GravityTargetKind.Prop).ToArray();
            foreach (var prop in props)
            {
                prop.RefreshGeometryCache();
                if (GravityArchitectureCollision.HasPenetration(prop, .003f))
                    throw new InvalidOperationException("Initial prop inside architecture: " + prop.name);
                foreach (var own in prop.ContactColliders)
                foreach (var renderer in models)
                {
                    var wall = renderer.GetComponent<BoxCollider>();
                    if (own.bounds.Intersects(wall.bounds) && Physics.ComputePenetration(own,own.transform.position,own.transform.rotation,
                            wall,wall.transform.position,wall.transform.rotation,out var direction,out float depth) && depth>.003f)
                        throw new InvalidOperationException($"Actual initial collider inside architecture: {prop.name} wall={wall.name} depth={depth:F4} direction={direction}");
                }
            }
            if (props.Length != 44) throw new InvalidOperationException("Expected 44 scene props");
            Debug.Log($"GRAVITY_ARCHITECTURE_AUDIT_PASS models={models.Length} buildings={models.Count(r=>r.GetComponent<GravityArchitectureSolid>().isBuilding)} fixtures={models.Count(r=>!r.GetComponent<GravityArchitectureSolid>().isBuilding)} props=44 initialWallPenetration=false individualModelColliders=true");
        }

        public static void Inspect()
        {
            EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            Physics.SyncTransforms();
            foreach (var r in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r => r.enabled && r.GetComponentInParent<GravityTarget>() == null
                    && r.bounds.size.y > 3f && r.bounds.size.x > 1f && r.bounds.size.z > 1f)
                .OrderBy(r => r.bounds.center.x).ThenBy(r => r.bounds.center.z))
                Debug.Log($"ARCHITECTURE_INVENTORY name={r.name} root={r.transform.root.name} centre={r.bounds.center:F3} size={r.bounds.size:F3} mesh={r.GetComponent<MeshFilter>()?.sharedMesh?.name} colliders={string.Join(",",r.GetComponents<Collider>().Select(c=>$"{c.GetType().Name}:{c.enabled}:{c.isTrigger}"))}");
            foreach (var target in UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None))
                Debug.Log($"ARCHITECTURE_TARGET name={target.name} kind={target.kind} centre={target.WorldBounds.center:F3} size={target.WorldBounds.size:F3}");
        }
    }
}
#endif
