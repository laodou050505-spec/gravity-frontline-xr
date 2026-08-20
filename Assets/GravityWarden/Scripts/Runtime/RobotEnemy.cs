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
            var game = FindFirstObjectByType<GravityGameManager>();
            player = game != null ? game.playerHead : Camera.main?.transform;
        }

        private void FixedUpdate()
        {
            if (!target.IsAlive || player == null) return;
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
                target.Body.linearVelocity = new Vector3(desired.x, target.Body.linearVelocity.y, desired.z);
            }
            if (flat.sqrMagnitude > .01f)
            {
                var rotation = RootRotationFacing(flat);
                transform.rotation = Quaternion.Slerp(transform.rotation, rotation, Time.fixedDeltaTime * 5f);
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
            var game = FindFirstObjectByType<GravityGameManager>();
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
