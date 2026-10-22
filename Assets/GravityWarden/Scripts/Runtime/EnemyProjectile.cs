using UnityEngine;

namespace GravityRepulsion
{
    [RequireComponent(typeof(GravityTarget))]
    public sealed class EnemyProjectile : MonoBehaviour
    {
        public float speed = 6.4f;
        public float life = 6f;
        public int playerDamage = 8;
        public Transform visualForward;
        public Vector3 modelForwardAxis = Vector3.forward;
        public Vector3 modelRotationOffset = new(0f, 0f, 0f);

        private GravityTarget target;
        private Vector3 direction;
        public GravityTarget Target => target;
        public Vector3 IntendedDirection { get; private set; }
        public float VelocityTargetDot { get; private set; }
        public float VisualVelocityDot { get; private set; }
        public float CameraNoseDot { get; private set; }

        private void Awake()
        {
            target = GetComponent<GravityTarget>();
        }

        public void Launch(Vector3 launchDirection)
        {
            direction = launchDirection.normalized;
            IntendedDirection = direction;
            target.Body.useGravity = false;
            target.Body.linearVelocity = direction * speed;
            FaceVelocity(direction);
            if (StopInsideArchitecture()) return;
            ValidateDirection("launch");
            GravityAudio.Play(GravitySound.Projectile, transform.position, .72f, 1.08f);
        }

        public void Reflect(Vector3 reflectDirection)
        {
            direction = reflectDirection.normalized;
            target.Body.linearVelocity = direction * 16f;
            target.MarkAsReflected();
            FaceVelocity(direction);
            if (StopInsideArchitecture()) return;
            IntendedDirection = direction;
            ValidateDirection("reflect");
            GravityFeedback.TargetPulse(target.Center, new Color(1f, .28f, .62f), .85f);
            GravityFeedback.Burst(target.Center, new Color(.35f, .78f, 1f), 12, .65f);
            GravityAudio.Play(GravitySound.Reflect, transform.position, .9f);
        }

        private void Update()
        {
            if (!target.IsAlive || target.IsHeld) return;
            if (target.Body.linearVelocity.sqrMagnitude > .05f) FaceVelocity(target.Body.linearVelocity.normalized);
            life -= Time.deltaTime;
            if (life <= 0f) target.DestroyTarget(DestroyReason.Default, false);
        }

        private void FixedUpdate()
        {
            if (!target.IsAlive || target.IsHeld || target.Body == null || target.Body.isKinematic) return;
            if (StopInsideArchitecture()) return;
            // Trigger projectiles do not receive physical wall response. Check the entire
            // next physics step, including reflected shots, before a fast missile can tunnel.
            var displacement = target.Body.linearVelocity * Time.fixedDeltaTime;
            if (!GravityArchitectureCollision.CastTarget(target, displacement, out var hit)) return;
            if (displacement.sqrMagnitude > .000001f)
            {
                var position = target.Body.position + displacement.normalized * Mathf.Max(0f, hit.distance - GravityArchitectureCollision.Skin);
                transform.position = position;
                target.Body.position = position;
            }
            HitArchitecture();
        }

        private bool StopInsideArchitecture()
        {
            if (target.IsHeld || !GravityArchitectureCollision.HasPenetration(target)) return false;
            GravityArchitectureCollision.ResolvePenetration(target);
            HitArchitecture();
            return true;
        }

        private void HitArchitecture()
        {
            if (!target.IsAlive || target.IsHeld) return;
            target.DestroyTarget(DestroyReason.Default, false);
        }

        private void FaceVelocity(Vector3 velocityDirection)
        {
            if (velocityDirection.sqrMagnitude < .001f) return;
            var axis = modelForwardAxis.sqrMagnitude > .001f ? modelForwardAxis.normalized : Vector3.forward;
            var axisCalibration = Quaternion.FromToRotation(axis, Vector3.forward);
            var desired = Quaternion.LookRotation(velocityDirection.normalized, Vector3.up) * axisCalibration * Quaternion.Euler(modelRotationOffset);
            (visualForward != null ? visualForward : transform).rotation = desired;
        }

        private void ValidateDirection(string phase)
        {
            var velocity = target.Body.linearVelocity;
            if (velocity.sqrMagnitude < .001f || IntendedDirection.sqrMagnitude < .001f) return;
            var visual = visualForward != null ? visualForward.TransformDirection(modelForwardAxis.normalized) : transform.forward;
            VelocityTargetDot = Vector3.Dot(velocity.normalized, IntendedDirection.normalized);
            VisualVelocityDot = Vector3.Dot(visual.normalized, velocity.normalized);
            var camera = Camera.main;
            var towardCamera = camera != null ? (camera.transform.position - transform.position).normalized : velocity.normalized;
            CameraNoseDot = Vector3.Dot(visual.normalized, towardCamera);
            var approachingCamera = camera == null || Vector3.Dot(velocity.normalized, towardCamera) > .8f;
            if (VelocityTargetDot < .98f || VisualVelocityDot < .98f || approachingCamera && CameraNoseDot < .98f)
                Debug.LogError($"GRAVITY_PROJECTILE_DIRECTION_FAIL phase={phase} object={name} velocityTargetDot={VelocityTargetDot:0.000} visualVelocityDot={VisualVelocityDot:0.000} cameraNoseDot={CameraNoseDot:0.000} noseAxis={modelForwardAxis}");
            else
                Debug.Log($"GRAVITY_PROJECTILE_DIRECTION_PASS phase={phase} object={name} velocityTargetDot={VelocityTargetDot:0.000} visualVelocityDot={VisualVelocityDot:0.000} cameraNoseDot={CameraNoseDot:0.000} noseAxis={modelForwardAxis}");
        }

        private void OnTriggerEnter(Collider other)
        {
            if (!target.IsAlive || target.IsHeld) return;
            if (other.GetComponentInParent<GravityArchitectureSolid>() != null)
            {
                HitArchitecture();
                return;
            }
            var game = other.GetComponentInParent<GravityPlayerShield>()?.game;
            if (game != null)
            {
                if (game.TryBlockProjectile(this)) return;
                game.DamagePlayer(playerDamage);
                target.DestroyTarget(DestroyReason.Default, false);
                return;
            }
            var robot = other.GetComponentInParent<GravityTarget>();
            if (robot != null && robot != target && robot.kind == GravityTargetKind.Robot && target.ThrownUntil > Time.time)
            {
                robot.Damage(2, DestroyReason.ReflectAttack);
                target.DestroyTarget(DestroyReason.Default, false);
            }
        }
    }

    public sealed class GravityPlayerShield : MonoBehaviour
    {
        public GravityGameManager game;
    }
}
