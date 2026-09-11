using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using UnityEngine;

namespace GravityRepulsion
{
    /// <summary>Opt-in physical regression; never runs during ordinary gameplay.</summary>
    public sealed class GravityGroundSmoke : MonoBehaviour
    {
        private bool failed;
        private int drops;
        private int regrabs;
        private int originalTriggerProps;
        private readonly WaitForFixedUpdate physicsStep = new WaitForFixedUpdate();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var args=Environment.GetCommandLineArgs();
            if ((!args.Contains("-gravityGroundSmoke") && !args.Contains("-gravityGroundPlacementEvidence"))
                || FindFirstObjectByType<GravityGroundSmoke>() != null) return;
            var go = new GameObject("GroundRegression");
            DontDestroyOnLoad(go);
            go.AddComponent<GravityGroundSmoke>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSeconds(2.5f);
            FindFirstObjectByType<GravityTitleController>()?.StartGame();
            yield return new WaitForSeconds(6f);
            var ground = GravityGroundSurface.Active;
            var guard = FindFirstObjectByType<GravitySceneStabilityGuard>();
            if (ground == null || guard == null || !guard.Completed || !guard.Passed)
            { Fail("ground_or_initial_stability"); yield break; }
            var props = FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(t => t.kind == GravityTargetKind.Prop).OrderBy(t => t.name).ToArray();
            originalTriggerProps = props.Count(t => t.SafeTriggerColliders.Count > 0);
            if (props.Length != 44 || originalTriggerProps < 7)
            { Fail($"expected_44_props_and_seven_original_trigger_props_actual={props.Length}/{originalTriggerProps}"); yield break; }

            // Isolate the physics observation from waves, projectiles and player input.
            var game = FindFirstObjectByType<GravityGameManager>();
            if (game != null) { game.StopAllCoroutines(); game.enabled = false; }
            foreach (var enemy in FindObjectsByType<RobotEnemy>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            foreach (var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) projectile.gameObject.SetActive(false);
            var hand = FindFirstObjectByType<GravityHand>();
            if (hand == null) { Fail("missing_hand"); yield break; }
            foreach (var playerHand in FindObjectsByType<GravityHand>(FindObjectsSortMode.None)) playerHand.enabled = false;

            var authoredSupports=FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c=>c.enabled && !c.isTrigger && c.attachedRigidbody==null
                    && (c.GetComponent<GravityGroundSurface>()!=null || IsRoad(c.name))).ToArray();
            foreach(var prop in props)
            {
                prop.RefreshGeometryCache();
                if(!TryVisualBounds(prop,out var visual)) { Fail("initial_visual_missing_"+prop.name);yield break; }
                float support=SupportHeight(prop,authoredSupports,ground.SurfaceY);
                if(visual.min.y<support-.006f || prop.ContactBounds.min.y<support-.006f)
                { Fail($"initial_buried_{prop.name}_visualBottom={visual.min.y:F4}_contactBottom={prop.ContactBounds.min.y:F4}_surface={support:F4}");yield break; }
                Debug.Log($"GROUND_INITIAL_RUNTIME_CLEAR name={prop.name} visualBottom={visual.min.y:F4} support={support:F4} initialTrigger={prop.ContactColliders.Any(c=>c.isTrigger)}");
            }
            if(Environment.GetCommandLineArgs().Contains("-gravityGroundPlacementEvidence"))
            {
                yield return CaptureOriginalCrates(props,hand,ground,authoredSupports);
                yield break;
            }

            var fixedItems = FindObjectsByType<Collider>(FindObjectsSortMode.None)
                .Where(c => c.enabled && !c.isTrigger && c.attachedRigidbody == null)
                .Select(c => c.transform).Distinct().ToDictionary(t => t, t => (t.position, t.rotation));
            var originalRotations = props.Select(p => p.transform.rotation).ToArray();
            var originalCenters = props.Select(p => p.ContactBounds.center).ToArray();
            foreach (float x in new[] { -80f, 0f, 80f }) foreach (float z in new[] { -70f, 0f, 80f })
                if (!ground.GetComponent<Collider>().Raycast(new Ray(new Vector3(x, 10, z), Vector3.down), out _, 20))
                { Fail($"missing_floor_at_{x}_{z}"); yield break; }

            // Original poses/locations exercise the real street, plinths, kerbs and steps.
            PlaceBatch(props, originalRotations, originalCenters, hand, ground, 0, false, .6f);
            yield return Observe(props, ground, "authored_street", 10f, false);
            if (failed) yield break;

            for (int phase = 1; phase <= 3; phase++)
            {
                PlaceBatch(props, originalRotations, originalCenters, hand, ground, phase, true, 3f);
                yield return Observe(props, ground, "ground_rotation_" + phase, 9f, true);
                if (failed) yield break;
            }

            // Raise a real collision platform under the whole test grid, then drop over its edge.
            var step = GameObject.CreatePrimitive(PrimitiveType.Cube);
            step.name = "REGRESSION_ONLY_RAISED_STEP";
            step.transform.position = new Vector3(0, ground.SurfaceY + .3f, -72f);
            step.transform.localScale = new Vector3(185f, .6f, 38f);
            step.GetComponent<Collider>().sharedMaterial = GravityTarget.RestingPropMaterial;
            Physics.SyncTransforms();
            PlaceBatch(props, originalRotations, originalCenters, hand, ground, 4, true, 3f);
            yield return Observe(props, ground, "raised_step", 9f, true);
            if (failed) yield break;
            foreach (var prop in props)
                if (prop.ContactBounds.min.y < ground.SurfaceY + .6f - .025f)
                { Fail("step_penetration_" + prop.name); yield break; }
            Destroy(step);
            yield return physicsStep;
            yield return Observe(props, ground, "step_to_street", 7f, true);
            if (failed) yield break;

            // Try driving every held prop through the floor, then release at 40 m/s.
            foreach (var prop in props)
            {
                prop.Grab(hand);
                var desired = prop.ContactBounds.center;
                desired.y = ground.SurfaceY - 8f;
                prop.FollowPose(desired, hand.holdAnchor.rotation, 10f);
                if (prop.ContactBounds.min.y < ground.SurfaceY - .008f)
                { Fail("held_below_ground_" + prop.name); yield break; }
                prop.Release(Vector3.down * 40f, new Vector3(.7f, .4f, -.3f));
                drops++;
            }
            yield return Observe(props, ground, "held_below_floor_release", 9f, true);
            if (failed) yield break;

            foreach (var pair in fixedItems)
                if (pair.Key != null && (Vector3.Distance(pair.Key.position, pair.Value.position) > .0001f
                    || Quaternion.Angle(pair.Key.rotation, pair.Value.rotation) > .01f))
                { Fail("static_scene_moved_" + pair.Key.name); yield break; }
            if (ground.FallRecoveries != 0) { Fail("ccd_missed_floor_recoveries=" + ground.FallRecoveries); yield break; }
            Debug.Log($"GRAVITY_GROUND_SMOKE_PASS props=44 originalTriggerProps={originalTriggerProps} drops={drops} regrabs={regrabs} phases=7 orientations=4 street=true raisedStep=true maxDropSpeed=40 heldBelowGround=true fixedArchitecture=true fallRecoveries=0 naturalSleep=true initialVisualGroundPenetration=false");
            Application.Quit(0);
        }

        private static bool IsRoad(string name) => name.StartsWith("tripo_convert_5c702779-7e2c-40f1-8f4f-b3c437f78809",StringComparison.Ordinal)
            || name.StartsWith("tripo_convert_ecaf77f1-63c8-45a6-ba1a-772fd148ae13",StringComparison.Ordinal);

        private static bool TryVisualBounds(GravityTarget prop,out Bounds bounds)
        {
            bounds=default; bool found=false;
            foreach(var renderer in prop.GetComponentsInChildren<Renderer>(true))
            {
                if(!(renderer is MeshRenderer || renderer is SkinnedMeshRenderer) || !renderer.enabled
                    || !renderer.gameObject.activeInHierarchy || renderer.GetComponentInParent<GravityTarget>()!=prop) continue;
                if(found)bounds.Encapsulate(renderer.bounds);else { bounds=renderer.bounds;found=true; }
            }
            return found;
        }
        private static float SupportHeight(GravityTarget prop,Collider[] supports,float floorY)
        {
            float height=floorY;
            var contact=prop.ContactBounds;
            for(int x=0;x<=8;x++)for(int z=0;z<=8;z++)
            {
                var ray=new Ray(new Vector3(Mathf.Lerp(contact.min.x+.001f,contact.max.x-.001f,x/8f),
                    Mathf.Max(contact.max.y+1f,2f),Mathf.Lerp(contact.min.z+.001f,contact.max.z-.001f,z/8f)),Vector3.down);
                if(!prop.ContactColliders.Any(c=>c!=null && c.enabled && c.Raycast(ray,out _,100f)))continue;
                foreach(var support in supports)
                    if(support.Raycast(ray,out var hit,100f) && hit.normal.y>.5f)height=Mathf.Max(height,hit.point.y);
            }
            return height;
        }

        private IEnumerator CaptureOriginalCrates(GravityTarget[] props,GravityHand hand,GravityGroundSurface ground,Collider[] supports)
        {
            var args=Environment.GetCommandLineArgs();
            int index=Array.IndexOf(args,"-gravityGroundPlacementOutput");
            string output=index>=0 && index+1<args.Length?args[index+1]:Path.GetFullPath(Path.Combine("Evidence","GroundPlacement0910"));
            Directory.CreateDirectory(output);
            foreach(var desktop in FindObjectsByType<DesktopFallbackController>(FindObjectsSortMode.None))desktop.enabled=false;
            var view=Camera.main;
            if(view==null){Fail("placement_camera_missing");yield break;}
            var hud=FindFirstObjectByType<GravityHud>();
            if(hud!=null)hud.gameObject.SetActive(false);
            var crates=props.Where(p=>p.name.StartsWith("tripo_convert_6524e622-a9af-4f19-9d44-d03d23a4ea5b",StringComparison.Ordinal)).ToArray();
            if(crates.Length!=12){Fail("placement_expected_12_crates");yield break;}
            Screen.SetResolution(1600,1000,false);
            var cameraObject=new GameObject("EVIDENCE_ONLY_CRATE_CAMERA");
            var camera=cameraObject.AddComponent<Camera>();camera.CopyFrom(view);
            camera.stereoTargetEye=StereoTargetEyeMask.None;camera.enabled=false;camera.fieldOfView=48f;
            for(int group=0;group<3;group++)
            {
                var selected=crates.Where(c=>group==0?c.Center.x>0:group==1?c.Center.x<0 && c.Center.z>0:c.Center.z<0).ToArray();
                if(selected.Length==0){Fail("crate_group_missing_"+group);yield break;}
                var bounds=selected[0].ContactBounds;
                foreach(var crate in selected.Skip(1))bounds.Encapsulate(crate.ContactBounds);
                var focus=bounds.center;
                float side=group==0?-1f:1f;
                float distance=group==1?3.6f:2.4f;
                camera.transform.position=focus+new Vector3(side*distance,distance*.72f,-distance*.65f);
                camera.transform.LookAt(focus);
                yield return new WaitForEndOfFrame();
                CaptureCamera(camera,Path.Combine(output,$"{group+1:00}_Crates_Initial.png"));
            }
            // Reproduce upright, side-on and upside-down releases at the pair's real
            // authored pavement positions; the user's example shows the bottom face upward.
            var pair=crates.Where(c=>c.Center.x>0).ToArray();
            var originalCenters=pair.Select(c=>c.Center).ToArray();
            var originalRotations=pair.Select(c=>c.transform.rotation).ToArray();
            for(int pose=0;pose<4;pose++)
            {
                for(int i=0;i<pair.Length;i++)
                {
                    var crate=pair[i];
                    crate.Grab(hand);
                    var destination=originalCenters[i]+Vector3.up*.65f;
                    var desiredRotation=Quaternion.AngleAxis(pose*90f,Vector3.right)*originalRotations[i];
                    var handRotation=desiredRotation*Quaternion.Inverse(crate.transform.rotation)*hand.holdAnchor.rotation;
                    crate.FollowPose(destination,handRotation,10f);
                    crate.Release(Vector3.zero,Vector3.zero);
                }
                yield return new WaitForSecondsRealtime(5f);
                foreach(var crate in pair)
                {
                    TryVisualBounds(crate,out var visual);
                    float support=SupportHeight(crate,supports,ground.SurfaceY);
                    if(visual.min.y<support-.025f || !crate.Body.IsSleeping())
                    {Fail($"crate_replaced_pose{pose*90}_buried_or_unsettled_{crate.name}_bottom={visual.min.y:F4}_support={support:F4}_sleep={crate.Body.IsSleeping()}");yield break;}
                    Debug.Log($"GROUND_CRATE_POSE_PASS degrees={pose*90} name={crate.name} visualBottom={visual.min.y:F4} support={support:F4} sleep={crate.Body.IsSleeping()}");
                }
                var pairBounds=pair[0].ContactBounds;foreach(var crate in pair.Skip(1))pairBounds.Encapsulate(crate.ContactBounds);
                camera.transform.position=pairBounds.center+new Vector3(-2.4f,1.728f,-1.56f);camera.transform.LookAt(pairBounds.center);
                yield return new WaitForEndOfFrame();
                CaptureCamera(camera,Path.Combine(output,$"{pose+4:00}_RightPair_Replaced_{pose*90:000}.png"));
            }
            Debug.Log($"GRAVITY_GROUND_PLACEMENT_EVIDENCE_PASS props=44 crates=12 groups=3 rightPairReplaced={pair.Length} releasePoses=4 releases={pair.Length*4} degrees=0,90,180,270 initialVisualGroundPenetration=false naturalSleep=true output={output}");
            Application.Quit(0);
        }
        private static void CaptureCamera(Camera camera,string file)
        {
            const int width=1600,height=1000;
            var target=RenderTexture.GetTemporary(width,height,24,RenderTextureFormat.ARGB32);
            var previous=RenderTexture.active;var priorTarget=camera.targetTexture;
            var pixels=new Texture2D(width,height,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture=target;camera.Render();RenderTexture.active=target;
                pixels.ReadPixels(new Rect(0,0,width,height),0,0);pixels.Apply();File.WriteAllBytes(file,pixels.EncodeToPNG());
            }
            finally
            {camera.targetTexture=priorTarget;RenderTexture.active=previous;Destroy(pixels);RenderTexture.ReleaseTemporary(target);}
        }

        private void PlaceBatch(GravityTarget[] props, Quaternion[] originalRotations, Vector3[] originalCenters,
            GravityHand hand, GravityGroundSurface ground, int phase, bool grid, float lift)
        {
            for (int i = 0; i < props.Length; i++)
            {
                var target = props[i];
                var rotation = phase switch
                {
                    1 => originalRotations[i],
                    2 => Quaternion.Euler(0, 43, 90) * originalRotations[i],
                    3 => Quaternion.Euler(180, -31, 0) * originalRotations[i],
                    4 => Quaternion.Euler(37, 23, 61) * originalRotations[i],
                    _ => originalRotations[i]
                };
                target.Grab(hand);
                regrabs++;
                target.transform.rotation = rotation;
                var position = grid ? new Vector3(-82f + (i % 11) * 16.4f, ground.SurfaceY, -84f + (i / 11) * 8f)
                    : originalCenters[i];
                var bounds = target.ContactBounds;
                position.y = grid ? ground.SurfaceY + lift + bounds.extents.y : originalCenters[i].y + lift;
                target.transform.position += position - bounds.center;
                target.Body.position = target.transform.position;
                target.Body.rotation = target.transform.rotation;
                target.RefreshGeometryCache(); // A live VFX child must never alter the physical bounds.
                target.Release(phase == 0 ? Vector3.zero : Vector3.down * (phase == 3 ? 40f : 12f),
                    phase == 4 ? new Vector3(1.2f, -.6f, .8f) : Vector3.zero);
                drops++;
            }
            Physics.SyncTransforms();
        }

        private IEnumerator Observe(GravityTarget[] props, GravityGroundSurface ground, string label, float duration, bool requireSleep)
        {
            var startCorrections = ground.PlacementCorrections;
            var tailStart = new Dictionary<GravityTarget, Vector3>();
            var lowest = props.ToDictionary(p => p, p => float.PositiveInfinity);
            var highest = props.ToDictionary(p => p, p => float.NegativeInfinity);
            float maxTailSpeed = 0, maxTailAngular = 0;
            for (float elapsed = 0; elapsed < duration; elapsed += Time.fixedDeltaTime)
            {
                yield return physicsStep;
                foreach (var prop in props)
                {
                    if (prop == null || !prop.IsAlive || prop.Body.isKinematic || !prop.Body.useGravity
                        || prop.ContactColliders.Any(c => c.enabled && c.isTrigger))
                    { Fail(label + "_disabled_physics_or_trigger_" + prop?.name); yield break; }
                    if (prop.PhysicalContactBounds.min.y < ground.SurfaceY - .035f)
                    {
                        var own = prop.ContactColliders[0];
                        var floor = ground.GetComponent<Collider>();
                        Physics.ComputePenetration(own, prop.Body.position, prop.Body.rotation,
                            floor, floor.transform.position, floor.transform.rotation, out var direction, out float depth);
                        Fail($"{label}_floor_penetration_{prop.name} elapsed={elapsed:0.000} physicalBottom={prop.PhysicalContactBounds.min.y:0.00000} visualBottom={prop.ContactBounds.min.y:0.00000} colliderBottom={own.bounds.min.y:0.00000} penetration={depth:0.00000} direction={direction} speed={prop.Body.linearVelocity} angular={prop.Body.angularVelocity} poseDelta={Vector3.Distance(prop.Body.position, prop.transform.position):0.00000}");
                        yield break;
                    }
                    if (elapsed < duration - 2f) continue;
                    if (!tailStart.ContainsKey(prop)) tailStart[prop] = prop.Body.position;
                    lowest[prop] = Mathf.Min(lowest[prop], prop.Body.position.y);
                    highest[prop] = Mathf.Max(highest[prop], prop.Body.position.y);
                    maxTailSpeed = Mathf.Max(maxTailSpeed, prop.Body.linearVelocity.magnitude);
                    maxTailAngular = Mathf.Max(maxTailAngular, prop.Body.angularVelocity.magnitude);
                }
            }
            if (ground.PlacementCorrections != startCorrections)
            { Fail(label + "_resting_body_was_teleported"); yield break; }
            int asleep = 0;
            foreach (var prop in props)
            {
                var drift = Vector3.Distance(tailStart[prop], prop.Body.position);
                var bounce = highest[prop] - lowest[prop];
                if (prop.Body.IsSleeping()) asleep++;
                if (drift > .04f || bounce > .025f || prop.Body.linearVelocity.magnitude > .10f || prop.Body.angularVelocity.magnitude > .15f
                    || (requireSleep && !prop.Body.IsSleeping()))
                { Fail($"{label}_unsettled_{prop.name}_drift={drift:0.000}_bounce={bounce:0.000}_speed={prop.Body.linearVelocity.magnitude:0.000}_angular={prop.Body.angularVelocity.magnitude:0.000}_sleep={prop.Body.IsSleeping()}"); yield break; }
            }
            Debug.Log($"GRAVITY_GROUND_PHASE_PASS phase={label} props=44 sleeping={asleep} maxTailSpeed={maxTailSpeed:0.000} maxTailAngular={maxTailAngular:0.000} restingTeleports=0");
        }

        private void Fail(string reason)
        {
            failed = true;
            Debug.LogError("GRAVITY_GROUND_SMOKE_FAIL " + reason);
            Application.Quit(3);
        }
    }
}
