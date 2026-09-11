using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace GravityRepulsion
{
    /// <summary>Exercises actual imported props against both street facades, only on explicit invocation.</summary>
    public sealed class GravityArchitectureSmoke : MonoBehaviour
    {
        private bool failed;
        private readonly WaitForFixedUpdate physicsStep = new WaitForFixedUpdate();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravityArchitectureSmoke")
                || FindFirstObjectByType<GravityArchitectureSmoke>() != null) return;
            var go = new GameObject("ArchitectureRegression");
            DontDestroyOnLoad(go);
            go.AddComponent<GravityArchitectureSmoke>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSeconds(2.5f);
            FindFirstObjectByType<GravityTitleController>()?.StartGame();
            yield return new WaitForSeconds(6f);
            var game = FindFirstObjectByType<GravityGameManager>();
            var guard = FindFirstObjectByType<GravitySceneStabilityGuard>();
            if (game == null || guard == null || !guard.Completed || !guard.Passed)
            { Fail("initial_stability"); yield break; }
            game.StopAllCoroutines(); game.enabled = false;
            foreach (var enemy in FindObjectsByType<RobotEnemy>(FindObjectsSortMode.None)) enemy.gameObject.SetActive(false);
            foreach (var projectile in FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) projectile.gameObject.SetActive(false);
            var hand = FindFirstObjectByType<GravityHand>();
            foreach (var playerHand in FindObjectsByType<GravityHand>(FindObjectsSortMode.None)) playerHand.enabled = false;
            var props = FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(p => p.kind == GravityTargetKind.Prop).OrderBy(p => p.name).ToArray();
            var architecture = FindObjectsByType<GravityArchitectureSolid>(FindObjectsSortMode.None);
            if (props.Length != 44 || architecture.Length != 50 || hand == null)
            { Fail($"inventory_props={props.Length}_architecture={architecture.Length}"); yield break; }
            foreach (var prop in props)
            {
                if (GravityArchitectureCollision.HasPenetration(prop, .003f))
                { Fail("initial_inside_wall_" + prop.name); yield break; }
                prop.gameObject.SetActive(false);
            }
            var fixedPoses = architecture.ToDictionary(a => a.transform, a => (a.transform.position,a.transform.rotation));
            var rightWall = architecture.Single(a => a.name == "tripo_convert_d98730f9-44a9-4977-a733-d89ec5ea03a1").GetComponent<BoxCollider>();
            var leftWall = architecture.Single(a => a.name == "tripo_convert_fda750d0-0933-4382-9bdd-c89f8461afbc").GetComponent<BoxCollider>();
            int sweeps = 0, releases = 0, slides = 0, rotations = 0;
            float maxPenetration = 0f;
            for (int i = 0; i < props.Length; i++)
            {
                var prop = props[i];
                prop.gameObject.SetActive(true);
                var originalRotation = prop.transform.rotation;
                var wall = i % 2 == 0 ? rightWall : leftWall;
                float side = i % 2 == 0 ? 1f : -1f;
                var inward = Vector3.right * side;
                float facade = side > 0 ? wall.bounds.min.x : wall.bounds.max.x;
                var startCenter = new Vector3(facade - side * 4.5f, 3f + prop.ContactBounds.extents.y, wall.bounds.center.z);
                Place(prop, startCenter, originalRotation);
                prop.Grab(hand);
                var beyond = startCenter;
                beyond.x = side > 0 ? wall.bounds.max.x + 8f : wall.bounds.min.x - 8f;
                prop.FollowPose(beyond, hand.holdAnchor.rotation, 10f);
                sweeps++;
                if (!CheckSide(prop, wall, side, "full_building_sweep")) yield break;
                for (int frame = 0; frame < 10; frame++)
                {
                    prop.FollowPose(beyond, hand.holdAnchor.rotation, .02f);
                    if (!CheckSide(prop, wall, side, "continued_wall_pressure")) yield break;
                }
                float previousZ = prop.ContactBounds.center.z;
                var slideCenter = beyond; slideCenter.z = previousZ + .45f;
                prop.FollowPose(slideCenter, hand.holdAnchor.rotation, 10f);
                if (!CheckSide(prop, wall, side, "wall_slide") || prop.ContactBounds.center.z - previousZ < .3f)
                { if (!failed) Fail("wall_slide_stuck_" + prop.name); yield break; }
                slides++;
                for (int rotation = 1; rotation <= 6; rotation++)
                {
                    prop.FollowPose(slideCenter, Quaternion.AngleAxis(rotation*30,Vector3.up)*hand.holdAnchor.rotation, .08f);
                    if (GravityArchitectureCollision.HasPenetration(prop, .003f))
                    { Fail("held_rotation_penetrated_" + prop.name); yield break; }
                }
                rotations++;
                prop.Release(Vector3.zero, Vector3.zero);
                Place(prop, startCenter, originalRotation);
                prop.Grab(hand);
                prop.Release(inward * 40f, new Vector3(.3f, .8f, -.4f));
                releases++;
                for (int frame = 0; frame < 25; frame++)
                {
                    yield return physicsStep;
                    if (!CheckSide(prop, wall, side, "thrown_40mps", .035f)) yield break;
                    foreach (var own in prop.ContactColliders)
                    {
                        var localPosition = prop.transform.InverseTransformPoint(own.transform.position);
                        var rotationOffset = Quaternion.Inverse(prop.transform.rotation) * own.transform.rotation;
                        if (!Physics.ComputePenetration(own, prop.Body.position + prop.Body.rotation*Vector3.Scale(localPosition,prop.transform.lossyScale),
                                prop.Body.rotation*rotationOffset, wall,wall.transform.position,wall.transform.rotation,out _,out float depth)) continue;
                        maxPenetration = Mathf.Max(maxPenetration,depth);
                        if (depth > .035f) { Fail($"dynamic_wall_depth={depth:0.0000}_{prop.name}"); yield break; }
                    }
                }
                prop.gameObject.SetActive(false);
            }
            // A gap between distinct buildings remains traversable: no synthetic wall across the whole side.
            // Select the smallest real prop after activation; inactive Collider bounds are empty.
            var probe = props.OrderBy(p =>
            {
                p.gameObject.SetActive(true);
                p.RefreshGeometryCache();
                var extent = Vector3.Scale(GravityArchitectureCollision.LocalEnvelope(p).extents,p.transform.lossyScale).magnitude;
                p.gameObject.SetActive(false);
                return extent;
            }).First();
            probe.gameObject.SetActive(true);
            Place(probe, new Vector3(0,6,39.5f),Quaternion.identity);
            probe.Grab(hand);
            var gapGoal = probe.Center + Vector3.left*20f;
            propFollow(probe,gapGoal,hand);
            if (Vector3.Distance(probe.Center,gapGoal)>.08f || GravityArchitectureCollision.HasPenetration(probe))
            { Fail($"real_building_gap_obstructed_actual={probe.Center:F3}_goal={gapGoal:F3}_shape={GravityArchitectureCollision.LocalEnvelope(probe).size:F3}"); yield break; }
            foreach (var pair in fixedPoses)
                if (Vector3.Distance(pair.Key.position,pair.Value.position)>.0001f || Quaternion.Angle(pair.Key.rotation,pair.Value.rotation)>.01f)
                { Fail("fixed_architecture_moved"); yield break; }
            Debug.Log($"GRAVITY_ARCHITECTURE_SMOKE_PASS props=44 architecture=50 heldSweeps={sweeps} repeatedPressure=true slides={slides} rotations={rotations} thrownReleases={releases} speed=40 initialPenetration=false maxDynamicPenetration={maxPenetration:0.0000} realGap=true fixedArchitecture=true");
            Application.Quit(0);
        }

        private static void propFollow(GravityTarget prop, Vector3 desired, GravityHand hand)
            => prop.FollowPose(desired, hand.holdAnchor.rotation,10f);

        private static void Place(GravityTarget prop, Vector3 center, Quaternion rotation)
        {
            prop.transform.rotation = rotation;
            prop.transform.position += center - prop.ContactBounds.center;
            prop.Body.position = prop.transform.position;
            prop.Body.rotation = rotation;
            prop.Body.linearVelocity = Vector3.zero;
            prop.Body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
        }

        private bool CheckSide(GravityTarget prop, BoxCollider wall, float side, string phase, float tolerance=.004f)
        {
            var bounds = prop.PhysicalContactBounds;
            float overlap = side > 0 ? bounds.max.x-wall.bounds.min.x : wall.bounds.max.x-bounds.min.x;
            // Rotated building boxes have an AABB corner closer than the actual facade at this Z.
            // Verify the real collider penetration and the original side, not its enclosing rectangle.
            float depth = 0;
            foreach (var own in prop.ContactColliders)
                if (Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                    wall, wall.transform.position, wall.transform.rotation,out _,out float candidate))
                    depth = Mathf.Max(depth,candidate);
            bool wrongSide = side > 0 ? bounds.center.x>wall.bounds.center.x : bounds.center.x<wall.bounds.center.x;
            if (depth <= tolerance && !wrongSide) return true;
            Fail($"{phase}_{prop.name}_depth={depth:0.0000}_aabbOverlap={overlap:0.0000}_wrongSide={wrongSide}_side={side}");
            return false;
        }

        private void Fail(string reason)
        {
            failed = true;
            Debug.LogError("GRAVITY_ARCHITECTURE_SMOKE_FAIL " + reason);
            Application.Quit(3);
        }
    }
}
