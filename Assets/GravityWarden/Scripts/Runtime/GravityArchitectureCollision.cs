using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GravityRepulsion
{
    /// <summary>Shape sweeps for poses that bypass PhysX (held objects, scripted powers and trigger projectiles).</summary>
    public static class GravityArchitectureCollision
    {
        public const float Skin = .012f;
        private static readonly RaycastHit[] castHits = new RaycastHit[256];
        private static readonly Collider[] overlaps = new Collider[256];
        private static BoxCollider queryShape;

        public static bool IsArchitecture(Collider collider) => collider != null && collider.enabled
            && !collider.isTrigger && collider.GetComponent<GravityArchitectureSolid>() != null;

        private static BoxCollider QueryShape
        {
            get
            {
                if (queryShape == null)
                {
                    var go = new GameObject("Architecture_QueryShape") { hideFlags = HideFlags.HideAndDontSave };
                    go.transform.position = new Vector3(0, -10000, 0);
                    queryShape = go.AddComponent<BoxCollider>();
                    queryShape.isTrigger = true;
                }
                return queryShape;
            }
        }

        public static Bounds LocalEnvelope(GravityTarget target)
        {
            // The visual envelope includes robot shoulders and held visuals, not only a narrow locomotion capsule.
            bool found = target.TryGetLocalGeometryBounds(target.transform, out var bounds);
            foreach (var collider in target.ContactColliders)
            {
                if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                Bounds local;
                if (collider is BoxCollider box) local = new Bounds(box.center, box.size);
                else if (collider is SphereCollider sphere) local = new Bounds(sphere.center, Vector3.one * sphere.radius * 2f);
                else if (collider is CapsuleCollider capsule)
                {
                    var size = Vector3.one * capsule.radius * 2f;
                    size[capsule.direction] = Mathf.Max(capsule.height, size[capsule.direction]);
                    local = new Bounds(capsule.center, size);
                }
                else if (collider is MeshCollider mesh && mesh.sharedMesh != null) local = mesh.sharedMesh.bounds;
                else continue;
                var matrix = target.transform.worldToLocalMatrix * collider.transform.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (found) bounds.Encapsulate(point); else { bounds = new Bounds(point, Vector3.zero); found = true; }
                }
            }
            return found ? bounds : new Bounds(Vector3.zero, Vector3.one * .6f);
        }

        private static void WorldEnvelope(GravityTarget target, out Vector3 center, out Vector3 extents)
        {
            var local = LocalEnvelope(target);
            center = target.transform.TransformPoint(local.center);
            var scale = target.transform.lossyScale;
            extents = Vector3.Scale(local.extents, new Vector3(Mathf.Abs(scale.x), Mathf.Abs(scale.y), Mathf.Abs(scale.z)));
            extents = Vector3.Max(extents, Vector3.one * .005f);
        }

        public static bool CastTarget(GravityTarget target, Vector3 displacement, out RaycastHit hit)
        {
            hit = default;
            if (target == null || displacement.sqrMagnitude < .00000001f) return false;
            WorldEnvelope(target, out var center, out var extents);
            float distance = displacement.magnitude;
            var direction = displacement / distance;
            int count = Physics.BoxCastNonAlloc(center, extents, direction, castHits, target.transform.rotation,
                distance + Skin, ~0, QueryTriggerInteraction.Ignore);
            float nearest = float.PositiveInfinity;
            for (int i = 0; i < count; i++)
            {
                var candidate = castHits[i];
                if (!IsArchitecture(candidate.collider) || candidate.distance >= nearest) continue;
                if (candidate.distance <= .0001f)
                {
                    // A cast starting at contact may report -castDirection, which would also block
                    // movement away from or parallel to the wall. Use the actual separation normal.
                    var shape = QueryShape;
                    shape.center = Vector3.zero;
                    shape.size = extents * 2f;
                    if (!Physics.ComputePenetration(shape, center, target.transform.rotation, candidate.collider,
                        candidate.collider.transform.position,candidate.collider.transform.rotation,out var normal,out _))
                    {
                        normal = center - candidate.collider.ClosestPoint(center);
                        if (normal.sqrMagnitude < .0000001f) continue;
                        normal.Normalize();
                    }
                    candidate.normal = normal;
                }
                if (Vector3.Dot(direction, candidate.normal) >= -.0001f) continue;
                nearest = candidate.distance;
                hit = candidate;
            }
            return nearest < float.PositiveInfinity;
        }

        public static bool HasPenetration(GravityTarget target, float tolerance = .002f)
            => FindCorrection(target, tolerance, out _);

        private static bool FindCorrection(GravityTarget target, float tolerance, out Vector3 correction)
        {
            correction = Vector3.zero;
            WorldEnvelope(target, out var center, out var extents);
            var shape = QueryShape;
            shape.center = Vector3.zero;
            shape.size = extents * 2f;
            int count = Physics.OverlapBoxNonAlloc(center, extents + Vector3.one * Skin, overlaps,
                target.transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            float deepest = tolerance;
            for (int i = 0; i < count; i++)
            {
                var other = overlaps[i];
                if (!IsArchitecture(other) || !Physics.ComputePenetration(shape, center, target.transform.rotation,
                        other, other.transform.position, other.transform.rotation, out var direction, out float depth)
                    || depth <= deepest) continue;
                var movement = direction * (depth + Skin);
                if (direction.y < -.5f)
                {
                    // An authored model at ground level must leave the wall sideways, never through the foundation.
                    var rotation = target.transform.rotation;
                    var x = rotation * new Vector3(extents.x, 0, 0);
                    var y = rotation * new Vector3(0, extents.y, 0);
                    var z = rotation * new Vector3(0, 0, extents.z);
                    var aabb = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                        Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y), Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
                    var b = other.bounds;
                    var right = b.max.x - (center.x-aabb.x) + Skin;
                    var left = center.x+aabb.x - b.min.x + Skin;
                    var forward = b.max.z - (center.z-aabb.z) + Skin;
                    var back = center.z+aabb.z - b.min.z + Skin;
                    float minimum = Mathf.Min(right, left, forward, back);
                    movement = minimum == right ? Vector3.right*right : minimum == left ? Vector3.left*left
                        : minimum == forward ? Vector3.forward*forward : Vector3.back*back;
                }
                deepest = depth;
                correction = movement;
            }
            return correction.sqrMagnitude > .0000001f;
        }

        public static bool ResolvePenetration(GravityTarget target)
        {
            if (target == null) return false;
            var original = target.transform.position;
            bool changed = false;
            for (int pass = 0; pass < 12; pass++)
            {
                if (!FindCorrection(target, .001f, out var correction)) break;
                SetPose(target, target.transform.position + correction, target.transform.rotation);
                RemoveInwardVelocity(target, correction.normalized);
                changed = true;
            }
            if (changed && HasPenetration(target, .001f))
            {
                // Adjacent facades can ask for opposite corrections. Solve the union once rather
                // than repeatedly pushing the object between two walls on subsequent frames.
                SetPose(target, original, target.transform.rotation);
                FindNearbyClearPlacement(target, original);
            }
            return changed;
        }

        private static bool FindNearbyClearPlacement(GravityTarget target, Vector3 original)
        {
            WorldEnvelope(target, out var center, out var localExtents);
            var rotation = target.transform.rotation;
            var x = rotation * new Vector3(localExtents.x, 0, 0);
            var y = rotation * new Vector3(0, localExtents.y, 0);
            var z = rotation * new Vector3(0, 0, localExtents.z);
            var extents = new Vector3(Mathf.Abs(x.x)+Mathf.Abs(y.x)+Mathf.Abs(z.x),
                Mathf.Abs(x.y)+Mathf.Abs(y.y)+Mathf.Abs(z.y),Mathf.Abs(x.z)+Mathf.Abs(y.z)+Mathf.Abs(z.z));
            var offset = center-original;
            int count = Physics.OverlapBoxNonAlloc(center,extents+new Vector3(12,0,12),overlaps,
                Quaternion.identity,~0,QueryTriggerInteraction.Ignore);
            // Candidate face positions are taken from individual nearby models; gaps remain open.
            var xs = new List<float> { original.x };
            var zs = new List<float> { original.z };
            for (int i=0;i<count;i++)
            {
                if (!IsArchitecture(overlaps[i])) continue;
                var b = overlaps[i].bounds;
                xs.Add(b.min.x-extents.x-offset.x-Skin*2);
                xs.Add(b.max.x+extents.x-offset.x+Skin*2);
                zs.Add(b.min.z-extents.z-offset.z-Skin*2);
                zs.Add(b.max.z+extents.z-offset.z+Skin*2);
            }
            var nearX=xs.Distinct().OrderBy(v=>Mathf.Abs(v-original.x)).Take(18).ToArray();
            var nearZ=zs.Distinct().OrderBy(v=>Mathf.Abs(v-original.z)).Take(18).ToArray();
            var candidates=nearX.SelectMany(px=>nearZ.Select(pz=>new Vector3(px,original.y,pz)))
                .OrderBy(p=>(p-original).sqrMagnitude);
            foreach (var position in candidates)
            {
                if ((position-original).sqrMagnitude>144f) break;
                SetPose(target,position,rotation);
                if (HasPenetration(target,.001f)) continue;
                RemoveInwardVelocity(target,(position-original).normalized);
                return true;
            }
            SetPose(target,original,rotation);
            return false;
        }

        public static bool MoveSafely(GravityTarget target, Vector3 desiredRoot, Quaternion desiredRotation)
        {
            if (target == null) return false;
            bool blocked = ResolvePenetration(target);
            var startPosition = target.transform.position;
            var startRotation = target.transform.rotation;
            int steps = Mathf.Max(1, Mathf.CeilToInt(Quaternion.Angle(startRotation, desiredRotation) / 3f));
            for (int step = 1; step <= steps; step++)
            {
                float t = (float)step / steps;
                var destination = Vector3.Lerp(startPosition, desiredRoot, t);
                var remaining = destination - target.transform.position;
                for (int slide = 0; slide < 3 && remaining.sqrMagnitude > .0000001f; slide++)
                {
                    if (!CastTarget(target, remaining, out var hit))
                    {
                        SetPose(target, target.transform.position + remaining, target.transform.rotation);
                        break;
                    }
                    blocked = true;
                    var travel = remaining.normalized * Mathf.Min(remaining.magnitude, Mathf.Max(0, hit.distance - Skin));
                    SetPose(target, target.transform.position + travel, target.transform.rotation);
                    remaining = Vector3.ProjectOnPlane(remaining - travel, hit.normal);
                    RemoveInwardVelocity(target, hit.normal);
                }
                var previous = target.transform.rotation;
                // An earlier blocked step must not accumulate into a later large rotation jump.
                var next = Quaternion.RotateTowards(previous, desiredRotation, 3f);
                SetPose(target, target.transform.position, next);
                if (!HasPenetration(target, .0002f)) continue;
                blocked = true;
                SetPose(target, target.transform.position, previous);
                // Keep the largest safe rotation, so objects can turn naturally alongside a facade.
                float lo = 0, hi = 1;
                for (int attempt = 0; attempt < 8; attempt++)
                {
                    float middle = (lo + hi) * .5f;
                    SetPose(target, target.transform.position, Quaternion.Slerp(previous, next, middle));
                    if (HasPenetration(target, .0002f)) hi = middle; else lo = middle;
                }
                SetPose(target, target.transform.position, Quaternion.Slerp(previous, next, lo));
            }
            return blocked;
        }

        private static void SetPose(GravityTarget target, Vector3 position, Quaternion rotation)
        {
            target.transform.SetPositionAndRotation(position, rotation);
            var body = target.Body != null ? target.Body : target.GetComponent<Rigidbody>();
            if (body == null) return;
            body.position = position;
            body.rotation = rotation;
        }

        private static void RemoveInwardVelocity(GravityTarget target, Vector3 normal)
        {
            var body = target.Body;
            if (body == null || body.isKinematic) return;
            float speed = Vector3.Dot(body.linearVelocity, normal);
            if (speed < 0) body.linearVelocity -= normal * speed;
        }
    }
}
