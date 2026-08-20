using System.Collections.Generic;
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
        public bool IsXRTracked { get; private set; }
        public Vector3 RayOriginPosition => rayOrigin != null ? rayOrigin.position : transform.position;
        public Ray AimRay => new Ray(RayOriginPosition, transform.forward);

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
        }

        private void Update()
        {
            UpdatePose();
            UpdateVelocity();
            var pressed = ReadGrabPressed();
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
            if (Held != null) return;
            var target = FindTarget();
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
        }

        public void ReleaseHeld(float throwSpeed = 9.5f)
        {
            if (Held == null) return;
            var target = Held;
            Held = null;
            var throwVelocity = IsXRTracked ? estimatedVelocity * 1.35f : AimRay.direction * throwSpeed + Vector3.up;
            if (target.kind == GravityTargetKind.Projectile) throwVelocity = AimRay.direction * 17f;
            target.Release(throwVelocity, estimatedAngularVelocity);
            Pulse(.2f, .05f);
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

        private GravityTarget FindTarget()
        {
            var direct = Physics.OverlapSphere(transform.position, directGrabRadius, targetMask, QueryTriggerInteraction.Collide)
                .Select(c => c.GetComponentInParent<GravityTarget>())
                .Where(IsValid)
                .Distinct()
                .OrderBy(t => Vector3.Distance(transform.position, t.transform.position))
                .FirstOrDefault();
            if (direct != null) return direct;

            var hits = Physics.SphereCastAll(AimRay, .22f, rayLength, targetMask, QueryTriggerInteraction.Collide);
            var candidates = new List<(GravityTarget target, int priority, float distance)>();
            foreach (var hit in hits)
            {
                var target = hit.collider.GetComponentInParent<GravityTarget>();
                if (!IsValid(target)) continue;
                var priority = target.kind switch
                {
                    GravityTargetKind.Robot => 0,
                    GravityTargetKind.Projectile => 1,
                    GravityTargetKind.Heal => 2,
                    _ => 3
                };
                candidates.Add((target, priority, hit.distance));
            }
            var selected = candidates.OrderBy(c => c.priority).ThenBy(c => c.distance).FirstOrDefault().target;
            if (selected != null) return selected;

            var game = FindFirstObjectByType<GravityGameManager>();
            if (game == null) return null;
            if (OtherHandHasRobot() && game.AliveEnemies.Count == 1) return game.NearestAvailableProp(transform.position);
            var alive = game.AliveEnemies.Where(t => t != null && t.IsAlive && !t.IsHeld).ToList();
            if (alive.Count <= 2 && alive.Count > 0)
                return alive.OrderByDescending(t => Vector3.Dot(AimRay.direction, (t.transform.position - AimRay.origin).normalized)).First();
            return null;
        }

        private bool OtherHandHasRobot()
        {
            var game = FindFirstObjectByType<GravityGameManager>();
            if (game == null) return false;
            var other = side == GravityHandSide.Left ? game.rightHand : game.leftHand;
            return other != null && other.Held != null && other.Held.kind == GravityTargetKind.Robot;
        }

        private static bool IsValid(GravityTarget target)
        {
            if (target == null || !target.IsAlive || target.IsHeld) return false;
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
            var candidate = Held == null ? FindTarget() : Held;
            if (Held == null && candidate != null && candidate != lastCandidate)
            {
                Pulse(.12f, .028f);
                GravityAudio.Play(GravitySound.Select, candidate.Center, .34f);
            }
            lastCandidate = candidate;
            var color = Held != null ? HeldColor : candidate != null ? TargetColor : IdleColor;
            if (glowMaterial != null)
            {
                glowMaterial.color = color;
                if (glowMaterial.HasProperty("_EmissionColor")) glowMaterial.SetColor("_EmissionColor", color * (Held != null ? 4f : 2f));
            }
            if (gravityRay == null) return;
            gravityRay.enabled = Held != null;
            if (Held == null)
            {
                if (rayMaterial != null) rayMaterial.color = new Color(IdleColor.r, IdleColor.g, IdleColor.b, 0f);
                return;
            }
            gravityRay.positionCount = 2;
            gravityRay.SetPosition(0, RayOriginPosition);
            gravityRay.SetPosition(1, Held.WorldBounds.center);
            gravityRay.startColor = color;
            gravityRay.endColor = new Color(color.r, color.g, color.b, Held != null ? .42f : .28f);
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
