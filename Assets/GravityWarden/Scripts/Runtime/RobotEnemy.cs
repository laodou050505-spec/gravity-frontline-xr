using UnityEngine;

namespace GravityRepulsion
{
    [RequireComponent(typeof(GravityTarget))]
    public sealed class RobotEnemy : MonoBehaviour
    {
        public float moveSpeed = 1.05f;
        public float attackDistance = 14f;
        public float attackInterval = 2.7f;
        public GameObject projectilePrefab;
        public GameObject alternateProjectilePrefab;
        public Transform muzzle;
        public Transform visualRoot;
        public Transform visualFacingProbe;
        public float visualYawOffset;
        public float visualPitchOffset;

        public Vector3 VisualFacingDirection => -transform.forward;

        public static Quaternion RootRotationFacing(Vector3 directionToPlayer)
        {
            directionToPlayer.y = 0f;
            return directionToPlayer.sqrMagnitude > .001f
                ? Quaternion.LookRotation(-directionToPlayer.normalized, Vector3.up)
                : Quaternion.identity;
        }

        private GravityTarget target;
        private GravityGameManager game;
        private Transform player;
        private float attackTimer;
        private bool alternateShot;

        private void Awake()
        {
            target = GetComponent<GravityTarget>();
            attackTimer = Random.Range(1.2f, 2.8f);
        }

        private void Start()
        {
            game = FindFirstObjectByType<GravityGameManager>();
            player = game != null ? game.playerHead : Camera.main?.transform;
        }

        private void FixedUpdate()
        {
            if (!target.IsAlive || player == null) return;
            // The singularity owns the safe pose while enemies are orbiting it.
            if (game != null && game.UltimateActive) return;
            if (target.IsHeld)
            {
                target.Body.linearVelocity = Vector3.zero;
                return;
            }

            var flat = player.position - transform.position;
            flat.y = 0f;
            var distance = flat.magnitude;
            if (distance > 5.3f)
            {
                var desired = flat.normalized * moveSpeed;
                // The visible robot is wider than its locomotion capsule. Sweep its full
                // shape so shoulders and equipment stop at facades too, retaining wall sliding.
                for (int pass = 0; pass < 2; pass++)
                {
                    if (!GravityArchitectureCollision.CastTarget(target, desired * Time.fixedDeltaTime, out var hit)) break;
                    var normal = Vector3.ProjectOnPlane(hit.normal, Vector3.up).normalized;
                    float inwardSpeed = Vector3.Dot(desired, normal);
                    if (normal.sqrMagnitude < .1f || inwardSpeed >= 0f) break;
                    desired -= normal * inwardSpeed;
                }
                target.Body.linearVelocity = new Vector3(desired.x, target.Body.linearVelocity.y, desired.z);
            }
            if (flat.sqrMagnitude > .01f)
            {
                var rotation = RootRotationFacing(flat);
                GravityArchitectureCollision.MoveSafely(target, target.Body.position,
                    Quaternion.Slerp(transform.rotation, rotation, Time.fixedDeltaTime * 5f));
            }
            attackTimer -= Time.fixedDeltaTime;
            if (distance <= attackDistance && attackTimer <= 0f)
            {
                Fire();
                attackTimer = attackInterval + Random.Range(-.2f, .5f);
            }
        }

        private void Fire()
        {
            game ??= FindFirstObjectByType<GravityGameManager>();
            if (game == null || !game.CanEnemyFire || projectilePrefab == null || player == null) return;
            var origin = muzzle != null ? muzzle.position : target.Center + transform.forward * .65f;
            var prefab = alternateShot && alternateProjectilePrefab != null ? alternateProjectilePrefab : projectilePrefab;
            alternateShot = !alternateShot;
            var projectile = Instantiate(prefab, origin, Quaternion.identity);
            projectile.GetComponent<EnemyProjectile>()?.Launch((player.position - origin).normalized);
            game.NotifyEnemyFired();
        }
    }
}
