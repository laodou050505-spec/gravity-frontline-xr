using UnityEngine;

namespace GravityRepulsion
{
    /// <summary>A permanent foundation and one-time safe placement for held/released bodies.</summary>
    [RequireComponent(typeof(BoxCollider))]
    public sealed class GravityGroundSurface : MonoBehaviour
    {
        public static GravityGroundSurface Active { get; private set; }
        private BoxCollider floor;
        private readonly Collider[] nearby = new Collider[128];
        public float SurfaceY => Floor.bounds.max.y;
        public int PlacementCorrections { get; private set; }
        public int FallRecoveries { get; private set; }
        private BoxCollider Floor => floor != null ? floor : floor = GetComponent<BoxCollider>();

        private void Awake()
        {
            Active = this;
            Floor.enabled = true;
            Floor.isTrigger = false;
            Floor.sharedMaterial = GravityTarget.RestingPropMaterial;
            Floor.contactOffset = .005f;
        }

        private void OnDestroy() { if (Active == this) Active = null; }

        private bool Supports(GravityTarget target)
        {
            if (target == null || !target.IsAlive || target.kind == GravityTargetKind.Projectile
                || target.kind == GravityTargetKind.Heal) return false;
            var bounds = target.ContactBounds;
            var ground = Floor.bounds;
            return bounds.max.x >= ground.min.x && bounds.min.x <= ground.max.x
                   && bounds.max.z >= ground.min.z && bounds.min.z <= ground.max.z;
        }

        public bool ResolvePenetration(GravityTarget target, float tolerance = .003f)
        {
            if (!Supports(target)) return false;
            bool corrected = false;
            // The foundation also catches a held pose driven completely under the street.
            var penetration = SurfaceY - target.ContactBounds.min.y;
            if (penetration > tolerance)
            {
                MoveTarget(target, Vector3.up * (penetration + .003f));
                corrected = true;
            }

            // Respect the actual geometry of raised pavements, steps and architecture.
            // This runs during held motion / solidification, never on resting contact frames.
            for (int pass = 0; pass < 6; pass++)
            {
                var bounds = target.ContactBounds;
                int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * .02f,
                    nearby, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
                float deepest = tolerance;
                var correction = Vector3.zero;
                foreach (var own in target.ContactColliders)
                {
                    if (own == null || !own.enabled || !own.gameObject.activeInHierarchy) continue;
                    for (int i = 0; i < count; i++)
                    {
                        var other = nearby[i];
                        if (other == null || other == own || other.attachedRigidbody != null
                            || other.transform.IsChildOf(target.transform)
                            || GravityArchitectureCollision.IsArchitecture(other)) continue;
                        if (!Physics.ComputePenetration(own, own.transform.position, own.transform.rotation,
                                other, other.transform.position, other.transform.rotation,
                                out var direction, out var distance) || distance <= deepest) continue;
                        deepest = distance;
                        correction = direction * (distance + .003f);
                    }
                }
                if (correction.sqrMagnitude < .0000001f) break;
                MoveTarget(target, correction);
                corrected = true;
            }
            if (corrected) PlacementCorrections++;
            return corrected;
        }

        public bool RecoverFallenTarget(GravityTarget target)
        {
            if (!Supports(target) || target.ContactBounds.max.y >= Floor.bounds.min.y - .2f) return false;
            FallRecoveries++;
            return ResolvePenetration(target);
        }

        private static void MoveTarget(GravityTarget target, Vector3 correction)
        {
            var position = target.transform.position + correction;
            target.transform.position = position;
            var body = target.Body;
            if (body == null) return;
            body.position = position;
            if (body.isKinematic) return;
            // Remove only velocity entering the surface; preserve throws along it.
            var direction = correction.normalized;
            float inwardSpeed = Vector3.Dot(body.linearVelocity, direction);
            if (inwardSpeed < 0f) body.linearVelocity -= direction * inwardSpeed;
        }
    }
}
