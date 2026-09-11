#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor.SceneManagement;
using UnityEngine;
namespace GravityRepulsion.Editor
{
    public static class GravityGroundAudit
    {
        public static void Run()
        {
            EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            Physics.SyncTransforms();
            var props = UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(t => t.kind == GravityTargetKind.Prop).ToArray();
            if (props.Length != 44) throw new InvalidOperationException("Ground audit requires all 44 scene props");
            var ground = UnityEngine.Object.FindFirstObjectByType<GravityGroundSurface>();
            if (ground == null || ground.GetComponent<Collider>().isTrigger) throw new InvalidOperationException("Permanent solid ground missing");
            var supports=GravityGroundUpgrade.GroundShapes(ground);
            foreach (var road in GravityGroundUpgrade.RoadModels())
            {
                var mesh=road.GetComponent<MeshCollider>();
                if(mesh==null || !mesh.enabled || mesh.isTrigger || mesh.convex
                    || mesh.sharedMesh!=road.GetComponent<MeshFilter>().sharedMesh
                    || road.GetComponents<Collider>().Count(c=>c.enabled)!=1)
                    throw new InvalidOperationException("Visible pavement must have one exact mesh contact: "+road.name);
            }
            foreach (var prop in props)
            {
                prop.RefreshGeometryCache();
                var box = prop.GetComponent<BoxCollider>();
                if (box == null || !prop.TryGetLocalGeometryBounds(box.transform, out var bounds)
                    || Vector3.Distance(box.center, bounds.center) > .001f || Vector3.Distance(box.size, bounds.size) > .002f)
                    throw new InvalidOperationException("Collider does not match the model in local coordinates: " + prop.name);
                var body = prop.GetComponent<Rigidbody>();
                if (body.constraints != RigidbodyConstraints.None || !body.isKinematic || body.useGravity)
                    throw new InvalidOperationException("Authored idle pose / free interaction changed: " + prop.name);
                if(!GravityGroundUpgrade.TryGetVisualBounds(prop,out var visual))
                    throw new InvalidOperationException("Initial prop visual bounds missing: "+prop.name);
                float support=GravityGroundUpgrade.SupportHeight(prop,supports,ground.SurfaceY);
                if(visual.min.y<support-.003f || prop.ContactBounds.min.y<support-.003f)
                    throw new InvalidOperationException($"Initial prop buried in visible street: {prop.name} visualBottom={visual.min.y:F4} contactBottom={prop.ContactBounds.min.y:F4} support={support:F4}");
                Debug.Log($"GROUND_INITIAL_CLEAR name={prop.name} visualBottom={visual.min.y:F4} contactBottom={prop.ContactBounds.min.y:F4} support={support:F4}");
                Debug.Log($"GROUND_PROP_LOCAL_FIT name={prop.name} box={box.size} model={bounds.size} importedRotation={prop.transform.eulerAngles} initialTrigger={box.isTrigger}");
            }
            var plane = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .FirstOrDefault(r => r.name == "Plane" && r.bounds.size.x > 100);
            if (plane == null || Mathf.Abs(plane.bounds.max.y - ground.SurfaceY) > .002f
                || plane.GetComponents<Collider>().Any(c => c.enabled))
                throw new InvalidOperationException("Visible ground height / duplicate collider mismatch");
            foreach (var r in UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(r => r.bounds.size.x > 10 && r.bounds.size.z > 10 && r.bounds.size.y < 4))
                Debug.Log($"GROUND_RENDERER name={r.name} centre={r.bounds.center} size={r.bounds.size} target={r.GetComponentInParent<GravityTarget>() != null} colliderCount={r.GetComponentsInChildren<Collider>().Length}");
            foreach (float x in new[] { -12f, -6f, 0f, 6f, 12f }) foreach (float z in new[] { -40f, -20f, 0f, 12f, 26f })
            {
                var hits = Physics.RaycastAll(new Vector3(x, 10, z), Vector3.down, 30, ~0, QueryTriggerInteraction.Ignore)
                    .Where(h => h.collider.attachedRigidbody == null && h.normal.y > .5f).OrderBy(h => h.distance).ToArray();
                Debug.Log($"GROUND_RAY x={x} z={z} hits={string.Join(";", hits.Select(h => $"{h.collider.name}@{h.point.y:0.000}"))}");
            }
            Debug.Log($"GRAVITY_GROUND_AUDIT_PASS props=44 safeTriggerProps={props.Count(p => p.SafeTriggerColliders.Count > 0)} geometryFit=localCoordinates floorTop={ground.SurfaceY:0.000} duplicatePlaneCollider=false freeBodyConstraints=true initialVisualGroundPenetration=false exactRoadMeshes={supports.Length-1}");
        }
    }
}
#endif
