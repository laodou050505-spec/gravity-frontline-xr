using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace GravityRepulsion
{
    public enum GravityHandSide { Left, Right }

    public sealed class GravityHand : MonoBehaviour
    {
        public GravityHandSide side;
        public Camera playerCamera;
        public Transform holdAnchor;
        public Transform rayOrigin;
        public LineRenderer gravityRay;
        public Renderer handGlow;
        public float rayLength = 45f;
        public float directGrabRadius = .45f;
        public LayerMask targetMask = ~0;

        public GravityTarget Held { get; private set; }
        public GravityTarget Candidate => Held != null ? Held : IsValid(lastCandidate) ? lastCandidate : null;
        public bool IsXRTracked { get; private set; }
        public float LastAimInputTime { get; private set; } = float.NegativeInfinity;
        public float LastGrabInputTime { get; private set; } = float.NegativeInfinity;
        public Vector3 RayOriginPosition => rayOrigin != null ? rayOrigin.position : transform.position;
        public Ray AimRay => new Ray(RayOriginPosition, rayOrigin != null ? rayOrigin.forward : transform.forward);
        public Vector3 AimPoint
        {
            get
            {
                var target = Held != null ? Held : IsValid(lastCandidate) ? lastCandidate : null;
                if (target != null) return target.WorldBounds.center;
                var ray = AimRay;
                foreach (var hit in Physics.RaycastAll(ray, rayLength, ~0, QueryTriggerInteraction.Collide).OrderBy(hit => hit.distance))
                {
                    if (IsAimOccluder(hit.collider)) return hit.point;
                }
                return ray.GetPoint(rayLength);
            }
        }

        private UnityEngine.XR.InputDevice device;
        private bool wasPressed;
        private Vector3 lastPosition;
        private Quaternion lastRotation;
        private Vector3 estimatedVelocity;
        private Vector3 estimatedAngularVelocity;
        private Material glowMaterial;
        private Material rayMaterial;
        private GravityTarget lastCandidate;
        private float nextHeldPulse;
        private bool usingPresentationSafePose;
        private Vector3 lastAimPosition;
        private Quaternion lastAimRotation;

        private static readonly Color IdleColor = new(.35f, .82f, 1f, .7f);
        private static readonly Color TargetColor = new(.78f, .35f, 1f, 1f);
        private static readonly Color HeldColor = new(1f, .38f, .72f, 1f);

        private void Awake()
        {
            playerCamera ??= Camera.main;
            if (holdAnchor == null)
            {
                var anchor = new GameObject("HoldAnchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(0f, 0f, .72f);
                holdAnchor = anchor.transform;
            }
            if (handGlow != null) glowMaterial = handGlow.material;
            if (gravityRay != null) rayMaterial = gravityRay.material;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
            lastAimPosition = transform.localPosition;
            lastAimRotation = transform.localRotation;
        }

        private void Update()
        {
            UpdatePose();
            UpdateVelocity();
            var pressed = ReadGrabPressed();
            if (pressed != wasPressed) LastGrabInputTime = Time.unscaledTime;
            if (pressed && !wasPressed) TryGrab();
            if (!pressed && wasPressed && Held != null) ReleaseHeld();
            wasPressed = pressed;
            UpdateFeedback();
        }

        private void LateUpdate()
        {
            if (Held != null)
            {
                Held.FollowPose(holdAnchor.position, holdAnchor.rotation, Time.deltaTime);
                if (Time.time >= nextHeldPulse)
                {
                    nextHeldPulse = Time.time + (Held.kind == GravityTargetKind.Robot ? .32f : .55f);
                    Pulse(Held.kind == GravityTargetKind.Robot ? .13f : .07f, .035f);
                }
            }
        }

        public void TryGrab()
        {
            LastGrabInputTime = Time.unscaledTime;
            if (Held != null) return;
            var target = FindAimTarget();
            if (target == null) return;
            if (target.kind == GravityTargetKind.Heal)
            {
                FindFirstObjectByType<GravityGameManager>()?.ConsumeHealthPack(target);
                Pulse(.45f, .12f);
                return;
            }
            if (!target.RequestGrab(this)) return;
            Held = target;
            Pulse(.32f, .08f);
            FindFirstObjectByType<GravityGameManager>()?.SetStatus(HudStatusEvent.Holding, $"HOLDING  {target.displayName} · [Release Grip] TO THROW");
        }

        public void ReleaseHeld(float throwSpeed = 9.5f)
        {
            LastGrabInputTime = Time.unscaledTime;
            if (Held == null) return;
            var target = Held;
            Held = null;
            var throwVelocity = IsXRTracked ? estimatedVelocity * 1.35f : AimRay.direction * throwSpeed + Vector3.up;
            if (target.kind == GravityTargetKind.Projectile) throwVelocity = AimRay.direction * 17f;
            target.Release(throwVelocity, estimatedAngularVelocity);
            Pulse(.2f, .05f);
            FindFirstObjectByType<GravityGameManager>()?.SetStatus(HudStatusEvent.Ready, $"RELEASED  {target.displayName}");
        }

        public void ForceForget(GravityTarget target)
        {
            if (Held == target) Held = null;
        }

        public void DebugGrab(GravityTarget target)
        {
            if (target == null || !target.IsAlive || target.IsHeld) return;
            Held = target;
            target.Grab(this);
        }

        public void PulsePattern(float amplitude, float duration, int count)
        {
            if (!isActiveAndEnabled) return;
            StartCoroutine(PulsePatternRoutine(amplitude, duration, count));
        }

        public void Pulse(float amplitude, float duration)
        {
            if (!device.isValid) return;
            device.SendHapticImpulse(0u, Mathf.Clamp01(amplitude), duration);
        }

        public GravityTarget FindAimTarget()
        {
            var ray = AimRay;
            // Keep touch-range grabs, but only in front of the hand and close to its
            // pointing axis. A nearby object behind the wrist must not steal the aim.
            var nearby = Physics.OverlapSphere(transform.position, directGrabRadius, targetMask, QueryTriggerInteraction.Collide)
                .OrderBy(collider => (collider.ClosestPoint(transform.position) - transform.position).sqrMagnitude);
            foreach (var collider in nearby)
            {
                var target = collider.GetComponentInParent<GravityTarget>();
                if (!IsValid(target)) continue;
                var point = collider.ClosestPoint(transform.position);
                var delta = point - transform.position;
                var along = Vector3.Dot(delta, ray.direction);
                if (along < 0f || Vector3.Dot(target.Center - transform.position, ray.direction) <= 0f) continue;
                if ((delta - ray.direction * along).sqrMagnitude > .22f * .22f) continue;
                if (HasClearPath(transform.position, point, target)) return target;
            }

            // Query every layer so walls still block an interaction when targetMask
            // only includes grabbable objects. The first real surface wins by distance.
            var hits = Physics.SphereCastAll(ray, .22f, rayLength, ~0, QueryTriggerInteraction.Collide)
                .OrderBy(hit => hit.distance);
            foreach (var hit in hits)
            {
                if (IsHandCollider(hit.collider)) continue;
                var target = hit.collider.GetComponentInParent<GravityTarget>();
                if (IsValid(target) && (targetMask.value & (1 << hit.collider.gameObject.layer)) != 0)
                {
                    // Unity reports a zero point for initial sphere overlaps. Those
                    // are handled by the directional touch-range query above.
                    if (hit.distance <= .0001f) continue;
                    if (HasClearPath(ray.origin, hit.point, target)) return target;
                }
                if (IsAimOccluder(hit.collider)) break;
            }
            return null;
        }

        private bool HasClearPath(Vector3 origin, Vector3 point, GravityTarget target)
        {
            var delta = point - origin;
            if (delta.sqrMagnitude < .000001f) return true;
            foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude + .005f, ~0, QueryTriggerInteraction.Collide).OrderBy(hit => hit.distance))
            {
                if (IsHandCollider(hit.collider)) continue;
                if (hit.collider.GetComponentInParent<GravityTarget>() == target) return true;
                if (IsAimOccluder(hit.collider)) return false;
            }
            return true;
        }

        private static bool IsHandCollider(Collider collider) => collider == null || collider.GetComponentInParent<GravityHand>() != null;

        private bool IsAimOccluder(Collider collider)
        {
            if (IsHandCollider(collider)) return false;
            var target = collider.GetComponentInParent<GravityTarget>();
            if (target != null && target.HeldBy == this) return false;
            return !collider.isTrigger || IsValid(target);
        }

        private static bool IsValid(GravityTarget target)
        {
            if (target == null || !target.isActiveAndEnabled || !target.IsAlive || target.IsHeld || target.IsScenePreview) return false;
            return target.kind != GravityTargetKind.Prop || target.HasValidVisibleInteractionGeometry(out _);
        }

        private void UpdatePose()
        {
            device = InputDevices.GetDeviceAtXRNode(side == GravityHandSide.Left ? XRNode.LeftHand : XRNode.RightHand);
            IsXRTracked = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.isTracked, out var tracked) && tracked;
            if (IsXRTracked && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.devicePosition, out var position) && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.deviceRotation, out var rotation))
            {
                // The PICO emulator can report a controller as tracked while its pose is at the
                // centre-eye (notably when virtual_input has not initialised).  Rendering the
                // wrist model at that pose puts the camera inside its geometry.  Keep a real
                // controller pose untouched, but use the same comfortable fallback presentation
                // pose whenever the reported controller is implausibly close to the eye.
                var trackedWorldPosition = transform.parent != null ? transform.parent.TransformPoint(position) : position;
                var eyeDistance = playerCamera != null ? Vector3.Distance(trackedWorldPosition, playerCamera.transform.position) : float.MaxValue;
                if (playerCamera != null && eyeDistance < .42f)
                {
                    ApplyPresentationSafePose();
                    // Only move the presentation model out of the eye. Its pointing
                    // rotation must continue to follow the tracked controller exactly.
                    transform.rotation = transform.parent != null ? transform.parent.rotation * rotation : rotation;
                    if (!usingPresentationSafePose)
                        Debug.Log($"GRAVITY_HAND_POSE_GUARD side={side} source=tracked eyeDistance={eyeDistance:0.000} fallback=true");
                    usingPresentationSafePose = true;
                    return;
                }
                transform.localPosition = position;
                transform.localRotation = rotation;
                if (usingPresentationSafePose)
                    Debug.Log($"GRAVITY_HAND_POSE_GUARD side={side} source=tracked eyeDistance={eyeDistance:0.000} fallback=false");
                usingPresentationSafePose = false;
                return;
            }
            IsXRTracked = false;
            if (playerCamera == null) return;
            usingPresentationSafePose = false;
            var mouseRay = Mouse.current != null ? playerCamera.ScreenPointToRay(Mouse.current.position.ReadValue()) : new Ray(playerCamera.transform.position, playerCamera.transform.forward);
            ApplyPresentationSafePose(mouseRay.direction);
        }

        private void ApplyPresentationSafePose(Vector3? aimDirection = null)
        {
            if (playerCamera == null) return;
            var offset = side == GravityHandSide.Left ? new Vector3(-.52f, -.42f, .75f) : new Vector3(.52f, -.42f, .75f);
            transform.position = playerCamera.transform.TransformPoint(offset);
            var direction = aimDirection ?? playerCamera.transform.forward;
            transform.rotation = Quaternion.LookRotation(direction, playerCamera.transform.up);
        }

        private void UpdateVelocity()
        {
            // Accumulate small movements against the last accepted pose, not the last
            // frame. Compare local poses so moving the player rig does not switch hands.
            if (Vector3.Distance(transform.localPosition, lastAimPosition) > .015f || Quaternion.Angle(transform.localRotation, lastAimRotation) > 2f)
            {
                LastAimInputTime = Time.unscaledTime;
                lastAimPosition = transform.localPosition;
                lastAimRotation = transform.localRotation;
            }
            var dt = Mathf.Max(.001f, Time.deltaTime);
            estimatedVelocity = Vector3.Lerp(estimatedVelocity, (transform.position - lastPosition) / dt, .45f);
            var delta = transform.rotation * Quaternion.Inverse(lastRotation);
            delta.ToAngleAxis(out var angle, out var axis);
            if (angle > 180f) angle -= 360f;
            estimatedAngularVelocity = axis * angle * Mathf.Deg2Rad / dt;
            lastPosition = transform.position;
            lastRotation = transform.rotation;
        }

        private bool ReadGrabPressed()
        {
            if (IsXRTracked)
            {
                var trigger = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.triggerButton, out var triggerValue) && triggerValue;
                var grip = device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.gripButton, out var gripValue) && gripValue;
                return trigger || grip;
            }
            var keyboard = Keyboard.current;
            if (keyboard == null) return false;
            return side == GravityHandSide.Left ? keyboard.qKey.isPressed : keyboard.eKey.isPressed;
        }

        private void UpdateFeedback()
        {
            var candidate = Held == null ? FindAimTarget() : Held;
            if (Held == null && candidate != null && candidate != lastCandidate)
            {
                Pulse(.12f, .028f);
                GravityAudio.Play(GravitySound.Select, candidate.Center, .34f);
                GravityFeedback.TargetPulse(candidate.Center, candidate.kind == GravityTargetKind.Heal
                    ? new Color(.2f, 1f, .55f)
                    : new Color(.55f, .35f, 1f), 1.25f);
                FindFirstObjectByType<GravityGameManager>()?.SetStatus(HudStatusEvent.Locked, $"LOCKED  {candidate.displayName} · [Pull Trigger] TO GRAB");
            }
            lastCandidate = candidate;
            var color = Held != null ? HeldColor : candidate != null ? TargetColor : IdleColor;
            if (glowMaterial != null)
            {
                glowMaterial.color = color;
                if (glowMaterial.HasProperty("_EmissionColor")) glowMaterial.SetColor("_EmissionColor", color * (Held != null ? 4f : 2f));
            }
            if (gravityRay == null) return;
            // Keep a thin aim ray visible while a target is only being hovered. The
            // previous behaviour showed the ray only after a successful grab, which
            // made XR aiming feel unresponsive and gave no confirmation of a valid hit.
            gravityRay.enabled = Held != null || candidate != null;
            if (Held == null && candidate == null)
            {
                if (rayMaterial != null) rayMaterial.color = new Color(IdleColor.r, IdleColor.g, IdleColor.b, 0f);
                return;
            }
            gravityRay.positionCount = 2;
            gravityRay.SetPosition(0, RayOriginPosition);
            gravityRay.SetPosition(1, (Held != null ? Held : candidate).WorldBounds.center);
            gravityRay.startColor = color;
            gravityRay.endColor = new Color(color.r, color.g, color.b, Held != null ? .48f : .34f);
            gravityRay.startWidth = Held != null ? .018f : .009f;
            gravityRay.endWidth = Held != null ? .008f : .004f;
            if (rayMaterial != null) rayMaterial.color = color;
        }

        private System.Collections.IEnumerator PulsePatternRoutine(float amplitude, float duration, int count)
        {
            for (var i = 0; i < count; i++)
            {
                Pulse(amplitude, duration);
                yield return new WaitForSeconds(duration + .035f);
            }
        }
    }
}
