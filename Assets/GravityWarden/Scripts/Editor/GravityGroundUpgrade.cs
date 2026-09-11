#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace GravityRepulsion.Editor
{
    public static class GravityGroundUpgrade
    {
        // These two authored assets are the street slabs and raised pavement visible in
        // the scene. Reuse their exact meshes, avoiding a second contact layer on the tiles.
        internal static bool IsRoadModel(string name) => name.StartsWith("tripo_convert_5c702779-7e2c-40f1-8f4f-b3c437f78809", StringComparison.Ordinal)
            || name.StartsWith("tripo_convert_ecaf77f1-63c8-45a6-ba1a-772fd148ae13", StringComparison.Ordinal);

        internal static MeshRenderer[] RoadModels() => UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
            .Where(r => r.enabled && r.gameObject.activeInHierarchy && r.GetComponentInParent<GravityTarget>() == null
                && IsRoadModel(r.name)).ToArray();

        private static int ConfigureVisibleRoadContacts()
        {
            var roads = RoadModels();
            foreach (var renderer in roads)
            {
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || renderer.bounds.size.y > 1f)
                    throw new InvalidOperationException("Visible pavement mesh changed: " + renderer.name);
                var mesh = renderer.GetComponent<MeshCollider>();
                if (mesh == null) mesh = renderer.gameObject.AddComponent<MeshCollider>();
                mesh.sharedMesh = filter.sharedMesh;
                mesh.convex = false; mesh.isTrigger = false; mesh.enabled = true; mesh.contactOffset = .005f;
                foreach (var old in renderer.GetComponents<Collider>())
                    if (old != mesh) { old.enabled = false; EditorUtility.SetDirty(old); }
                renderer.gameObject.isStatic = true;
                EditorUtility.SetDirty(mesh);
                Debug.Log($"GROUND_ROAD_MESH name={renderer.name} mesh={filter.sharedMesh.name} top={renderer.bounds.max.y:F4} exactVisibleMesh=true contacts=1");
            }
            if (roads.Length == 0) throw new InvalidOperationException("Authored pavement models are missing");
            return roads.Length;
        }

        internal static Collider[] GroundShapes(GravityGroundSurface ground) => RoadModels()
            .Select(r => r.GetComponent<MeshCollider>()).Where(c => c != null && c.enabled && !c.isTrigger)
            .Cast<Collider>().Append(ground.GetComponent<Collider>()).ToArray();

        internal static bool TryGetVisualBounds(GravityTarget prop, out Bounds bounds)
        {
            bounds=default; bool found=false;
            foreach (var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if (!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy || renderer.GetComponentInParent<GravityTarget>() != prop) continue;
                if (found) bounds.Encapsulate(renderer.bounds); else { bounds=renderer.bounds; found=true; }
            }
            return found;
        }

        internal static float SupportHeight(GravityTarget prop, Collider[] supports, float floorY)
        {
            float height=floorY;
            var contact=prop.ContactBounds;
            // Sample the object's actual projected footprint, rather than the empty corners
            // of its rotated world AABB. MeshCollider rays resolve raised tile relief too.
            for (int x=0;x<=8;x++) for (int z=0;z<=8;z++)
            {
                var ray=new Ray(new Vector3(Mathf.Lerp(contact.min.x+.001f,contact.max.x-.001f,x/8f),
                    Mathf.Max(contact.max.y+1f,2f),Mathf.Lerp(contact.min.z+.001f,contact.max.z-.001f,z/8f)),Vector3.down);
                if (!prop.ContactColliders.Any(c => c != null && c.enabled && c.Raycast(ray,out _,100f))) continue;
                foreach (var support in supports)
                    if (support.Raycast(ray,out var hit,100f) && hit.normal.y>.5f)
                        height=Mathf.Max(height,hit.point.y);
            }
            return height;
        }

        public static void InspectPlacement()
        {
            EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            Physics.SyncTransforms();
            var ground=UnityEngine.Object.FindFirstObjectByType<GravityGroundSurface>();
            var supports=GroundShapes(ground);
            foreach (var prop in UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None).Where(p=>p.kind==GravityTargetKind.Prop))
            {
                prop.RefreshGeometryCache();
                if (!TryGetVisualBounds(prop,out var visual)) continue;
                float support=SupportHeight(prop,supports,ground.SurfaceY);
                Debug.Log($"GROUND_PLACEMENT_INSPECT name={prop.name} centre={visual.center:F4} visualBottom={visual.min.y:F4} contactBottom={prop.ContactBounds.min.y:F4} support={support:F4} buried={support-visual.min.y:F4}");
                if(!prop.name.StartsWith("tripo_convert_6524e622",StringComparison.Ordinal))continue;
                var ray=new Ray(new Vector3(visual.center.x,20,visual.center.z),Vector3.down);
                foreach(var hit in Physics.RaycastAll(ray,30,~0,QueryTriggerInteraction.Collide).OrderBy(h=>h.point.y))
                    if(hit.collider.GetComponentInParent<GravityTarget>()==null)
                        Debug.Log($"CRATE_SUPPORT_CONTACT crate={prop.name} surface={hit.collider.name} y={hit.point.y:F4} type={hit.collider.GetType().Name} trigger={hit.collider.isTrigger} normal={hit.normal:F3}");
                foreach(var renderer in UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
                {
                    var rb=renderer.bounds;
                    if(!renderer.enabled || renderer.GetComponentInParent<GravityTarget>()!=null
                        || rb.min.x>visual.max.x || rb.max.x<visual.min.x || rb.min.z>visual.max.z || rb.max.z<visual.min.z
                        || rb.min.y>visual.max.y || rb.max.y<ground.SurfaceY-.1f)continue;
                    Debug.Log($"CRATE_SUPPORT_VISUAL crate={prop.name} surface={renderer.name} centre={rb.center:F4} size={rb.size:F4} contacts={string.Join(",",renderer.GetComponents<Collider>().Select(c=>$"{c.GetType().Name}:{c.enabled}:{c.isTrigger}"))}");
                }
            }
        }

        public static void Apply()
        {
            var scene = EditorSceneManager.OpenScene(GravityExistingSceneUpgrader.ScenePath);
            var plane = UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None)
                .Where(r=>r.name=="Plane" && r.bounds.size.x>40 && r.bounds.size.z>40 && r.bounds.size.y<.1f && r.GetComponentInParent<GravityTarget>()==null)
                .OrderByDescending(r=>r.bounds.size.x*r.bounds.size.z).FirstOrDefault();
            if (plane == null) throw new InvalidOperationException("Cannot identify existing visible ground; refusing guessed height");
            var bounds=plane.bounds;
            float sx=200f/bounds.size.x, sz=200f/bounds.size.z;
            var scale=plane.transform.localScale; scale.x*=sx; scale.z*=sz; plane.transform.localScale=scale;
            plane.enabled=true; plane.gameObject.SetActive(true); plane.gameObject.isStatic=true;
            if (Mathf.Abs(sx-1)> .001f || Mathf.Abs(sz-1)> .001f)
            {
                var materials=plane.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var old=materials[i]; if(old==null)continue;
                    string path=$"Assets/GravityWarden/Resources/GravityUI/GroundExtended_{i}.mat";
                    var m=AssetDatabase.LoadAssetAtPath<Material>(path);
                    if(m==null){m=new Material(old);AssetDatabase.CreateAsset(m,path);}
                    foreach(string prop in new[]{"_MainTex","_BaseMap"}) if(m.HasProperty(prop))
                    {var uv=old.GetTextureScale(prop);m.SetTextureScale(prop,new Vector2(uv.x*sx,uv.y*sz));}
                    materials[i]=m;EditorUtility.SetDirty(m);
                }
                plane.sharedMaterials=materials;
            }
            foreach(var old in plane.GetComponents<Collider>()) old.enabled=false;
            var go=GameObject.Find("GROUND_PERSISTENT_SOLID") ?? new GameObject("GROUND_PERSISTENT_SOLID");
            go.transform.position=new Vector3(bounds.center.x,bounds.max.y-1f,bounds.center.z);
            go.transform.rotation=Quaternion.identity;go.transform.localScale=Vector3.one;
            var collider=go.GetComponent<BoxCollider>(); if(collider==null)collider=go.AddComponent<BoxCollider>();
            collider.center=Vector3.zero;collider.size=new Vector3(200f,2f,200f);collider.isTrigger=false;collider.enabled=true;
            if(go.GetComponent<GravityGroundSurface>()==null)go.AddComponent<GravityGroundSurface>();
            go.isStatic=true;go.SetActive(true);
            var props = UnityEngine.Object.FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(t => t.kind == GravityTargetKind.Prop).ToArray();
            if (props.Length != 44) throw new InvalidOperationException("Expected 44 original scene props, got " + props.Length);
            int correctedBoxes = 0;
            foreach (var target in props)
            {
                var boxes = target.GetComponentsInChildren<BoxCollider>(true);
                var oldSizes = boxes.Select(box => box.size).ToArray();
                target.RefreshGeometryCache();
                target.ConfigurePropContactGeometry();
                for (int i = 0; i < boxes.Length; i++)
                {
                    if (Vector3.Distance(oldSizes[i], boxes[i].size) > .001f) correctedBoxes++;
                    // The shared runtime contact material is intentionally created on Awake.
                    boxes[i].sharedMaterial = null;
                    EditorUtility.SetDirty(boxes[i]);
                }
                EditorUtility.SetDirty(target.GetComponent<Rigidbody>());
                EditorUtility.SetDirty(target);
            }
            int roadMeshes = ConfigureVisibleRoadContacts();
            Physics.SyncTransforms();
            int raisedProps = 0;
            var supports = GroundShapes(go.GetComponent<GravityGroundSurface>());
            foreach (var target in props)
            {
                if (!TryGetVisualBounds(target, out var visual))
                    throw new InvalidOperationException("No visible geometry for initial ground placement: " + target.name);
                float support = SupportHeight(target, supports, bounds.max.y);
                float bottom = Mathf.Min(visual.min.y, target.ContactBounds.min.y);
                if (bottom >= support - .002f) continue;
                float lift = support + .006f - bottom;
                target.transform.position += Vector3.up * lift;
                var body = target.GetComponent<Rigidbody>();
                body.position = target.transform.position;
                body.rotation = target.transform.rotation;
                EditorUtility.SetDirty(target.transform);
                EditorUtility.SetDirty(body);
                PrefabUtility.RecordPrefabInstancePropertyModifications(target.transform);
                raisedProps++;
                Debug.Log($"GROUND_INITIAL_RAISED name={target.name} visualBottomBefore={visual.min.y:F4} support={support:F4} lift={lift:F4} preservedXZ=true");
            }
            Physics.SyncTransforms();
            var allColliders = UnityEngine.Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
            int additionallyProtected = 0;
            foreach (var target in props)
            {
                if (target.SafeTriggerColliders.Count > 0) continue;
                var own = target.GetComponentsInChildren<Collider>(true);
                bool overlaps = own.Any(a => allColliders.Any(b => a != b && b.enabled && !b.isTrigger
                    && !b.transform.IsChildOf(target.transform) && a.bounds.Intersects(b.bounds)
                    && Physics.ComputePenetration(a, a.transform.position, a.transform.rotation,
                        b, b.transform.position, b.transform.rotation, out _, out float distance) && distance > .035f));
                if (!overlaps) continue;
                // Preserve only non-ground overlap protection; initial ground placement was corrected above.
                target.ConfigureSafeTriggerColliders(own);
                foreach (var propCollider in own) { propCollider.isTrigger = true; EditorUtility.SetDirty(propCollider); }
                EditorUtility.SetDirty(target);
                additionallyProtected++;
            }
            Debug.Log($"GRAVITY_GROUND_CONTACTS_PASS props={props.Length} correctedBoxes={correctedBoxes} safeTriggerProps={props.Count(t => t.SafeTriggerColliders.Count > 0)} additionalInitialProtection={additionallyProtected} roadMeshes={roadMeshes} raisedInitialProps={raisedProps} initialGroundPenetration=false localGeometry=true restingPoseTeleports=false");
            EditorUtility.SetDirty(plane);EditorUtility.SetDirty(go);Physics.SyncTransforms();
            EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
            Debug.Log($"GRAVITY_GROUND_UPGRADE_PASS visual=existing_ground_reused size=200x200 thickness=2 topY={bounds.max.y:0.000} pullable=false");
        }
    }
}
#endif
