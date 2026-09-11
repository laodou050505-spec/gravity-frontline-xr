using UnityEngine;

namespace GravityRepulsion
{
    public sealed class FacePlayerBillboard : MonoBehaviour
    {
        public Transform visualRoot;
        public float yawOffset;
        public float pitchOffset;
        public bool lockVertical = true;
        public Vector3 VisualFacingDirection => visualRoot != null ? visualRoot.TransformDirection(Vector3.left) : transform.forward;
        public float PlayerFacingDot { get; private set; }

        private Transform player;
        private GravityTarget target;
        private bool runtimeFacingReported;

        private void Start()
        {
            player = FindFirstObjectByType<GravityGameManager>()?.playerHead ?? Camera.main?.transform;
            target = GetComponent<GravityTarget>();
        }

        private void LateUpdate()
        {
            if (player == null) return;
            var direction = player.position - transform.position;
            if (lockVertical) direction.y = 0f;
            if (direction.sqrMagnitude < .001f) return;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            if (visualRoot != null) visualRoot.localRotation = Quaternion.Euler(pitchOffset, yawOffset, 0f);
            // Billboarding changes the whole pickup footprint even when its rigidbody sleeps.
            if (target != null && target.IsAlive)
            {
                GravityArchitectureCollision.ResolvePenetration(target);
                target.Body.position = transform.position;
                target.Body.rotation = transform.rotation;
            }
            PlayerFacingDot = Vector3.Dot(VisualFacingDirection.normalized, direction.normalized);
            if (runtimeFacingReported) return;
            runtimeFacingReported = true;
            if (PlayerFacingDot < .98f)
                Debug.LogError($"GRAVITY_HEALTH_RUNTIME_FACING_FAIL object={name} dot={PlayerFacingDot:0.000} yawOffset={yawOffset:0.0}");
            else
                Debug.Log($"GRAVITY_HEALTH_RUNTIME_FACING_PASS object={name} dot={PlayerFacingDot:0.000} yawOffset={yawOffset:0.0}");
        }
    }
}
