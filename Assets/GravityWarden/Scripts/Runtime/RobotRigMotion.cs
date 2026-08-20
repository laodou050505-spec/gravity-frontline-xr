using System;
using UnityEngine;

namespace GravityRepulsion
{
    [RequireComponent(typeof(GravityTarget), typeof(RobotEnemy))]
    public sealed class RobotRigMotion : MonoBehaviour
    {
        public Transform rigRoot;
        public float strideLength = 1.05f;
        public float legSwingDegrees = 24f;
        public float armSwingDegrees = 17f;
        public float heldSwayPitchDegrees = 9f;
        public float heldSwayRollDegrees = 11f;

        public bool HasRig => leftThigh.IsValid && rightThigh.IsValid && leftUpperArm.IsValid && rightUpperArm.IsValid && pelvis.IsValid && spine01.IsValid;
        public bool IsWalkAnimating => walkBlend > .15f && target != null && !target.IsHeld;
        public bool IsHeldSwayAnimating => heldBlend > .15f && target != null && target.IsHeld;
        public float CurrentWalkBoneAngle { get; private set; }
        public float CurrentHeldVisualAngle { get; private set; }
        public float WalkPhase => walkPhase;

        private GravityTarget target;
        private RobotEnemy enemy;
        private float walkPhase;
        private float walkBlend;
        private float heldBlend;
        private float motionSeed;
        private BonePose pelvis;
        private BonePose spine01;
        private BonePose spine02;
        private BonePose leftThigh;
        private BonePose rightThigh;
        private BonePose leftCalf;
        private BonePose rightCalf;
        private BonePose leftFoot;
        private BonePose rightFoot;
        private BonePose leftUpperArm;
        private BonePose rightUpperArm;
        private BonePose leftForearm;
        private BonePose rightForearm;
        private float debugPoseUntil;
        private float debugPoseTime;
        private bool debugPoseDeterministicHeld;

        private struct BonePose
        {
            public Transform bone;
            public Quaternion bindRotation;
            public Vector3 bindPosition;
            public bool IsValid => bone != null;
        }

        private void Awake()
        {
            target = GetComponent<GravityTarget>();
            enemy = GetComponent<RobotEnemy>();
            rigRoot ??= enemy.visualRoot;
            motionSeed = UnityEngine.Random.Range(0f, 20f);
            CacheRig();
        }

        private void LateUpdate()
        {
            if (target == null || !target.IsAlive || !HasRig) return;
            if (Time.time < debugPoseUntil)
            {
                ApplyPose(debugPoseTime, debugPoseDeterministicHeld);
                return;
            }
            var horizontalVelocity = Vector3.ProjectOnPlane(target.Body.linearVelocity, Vector3.up);
            var moving = !target.IsHeld && horizontalVelocity.magnitude > .08f;
            walkBlend = Mathf.MoveTowards(walkBlend, moving ? 1f : 0f, Time.deltaTime * 5.5f);
            heldBlend = Mathf.MoveTowards(heldBlend, target.IsHeld ? 1f : 0f, Time.deltaTime * 7f);
            if (moving)
            {
                var cyclesPerSecond = horizontalVelocity.magnitude / Mathf.Max(.45f, strideLength);
                walkPhase += cyclesPerSecond * Mathf.PI * 2f * Time.deltaTime;
            }
            ApplyPose(Time.time, false);
        }

        public void DebugSetPose(float normalizedPhase, bool walking, bool held)
        {
            target ??= GetComponent<GravityTarget>();
            enemy ??= GetComponent<RobotEnemy>();
            rigRoot ??= enemy.visualRoot;
            CacheRig();
            walkPhase = normalizedPhase * Mathf.PI * 2f;
            walkBlend = walking ? 1f : 0f;
            heldBlend = held ? 1f : 0f;
            debugPoseTime = normalizedPhase * Mathf.PI * 2f;
            debugPoseDeterministicHeld = held;
            debugPoseUntil = Time.time + .32f;
            ApplyPose(debugPoseTime, held);
        }

        private void CacheRig()
        {
            if (rigRoot == null || HasRig) return;
            pelvis = Capture("Pelvis");
            spine01 = Capture("Spine01");
            spine02 = Capture("Spine02");
            leftThigh = Capture("L_Thigh");
            rightThigh = Capture("R_Thigh");
            leftCalf = Capture("L_Calf");
            rightCalf = Capture("R_Calf");
            leftFoot = Capture("L_Foot");
            rightFoot = Capture("R_Foot");
            leftUpperArm = Capture("L_Upperarm");
            rightUpperArm = Capture("R_Upperarm");
            leftForearm = Capture("L_Forearm");
            rightForearm = Capture("R_Forearm");
        }

        private BonePose Capture(string boneName)
        {
            var bone = FindDeepChild(rigRoot, boneName);
            return new BonePose { bone = bone, bindRotation = bone != null ? bone.localRotation : Quaternion.identity, bindPosition = bone != null ? bone.localPosition : Vector3.zero };
        }

        private void ApplyPose(float time, bool deterministicHeld)
        {
            RestoreAll();
            var stride = Mathf.Sin(walkPhase) * legSwingDegrees * walkBlend;
            var leftKnee = Mathf.Max(0f, -Mathf.Sin(walkPhase)) * legSwingDegrees * .62f * walkBlend;
            var rightKnee = Mathf.Max(0f, Mathf.Sin(walkPhase)) * legSwingDegrees * .62f * walkBlend;
            Pose(leftThigh, new Vector3(stride, 0f, 0f));
            Pose(rightThigh, new Vector3(-stride, 0f, 0f));
            Pose(leftCalf, new Vector3(leftKnee, 0f, 0f));
            Pose(rightCalf, new Vector3(rightKnee, 0f, 0f));
            Pose(leftFoot, new Vector3(-leftKnee * .42f, 0f, 0f));
            Pose(rightFoot, new Vector3(-rightKnee * .42f, 0f, 0f));
            Pose(leftUpperArm, new Vector3(-stride / Mathf.Max(1f, legSwingDegrees) * armSwingDegrees, 0f, 0f));
            Pose(rightUpperArm, new Vector3(stride / Mathf.Max(1f, legSwingDegrees) * armSwingDegrees, 0f, 0f));
            Pose(leftForearm, new Vector3(Mathf.Max(0f, stride) * .22f, 0f, 0f));
            Pose(rightForearm, new Vector3(Mathf.Max(0f, -stride) * .22f, 0f, 0f));

            var swayPitch = (deterministicHeld ? Mathf.Sin(time) : Mathf.Sin(time * 3.15f + motionSeed)) * heldSwayPitchDegrees * heldBlend;
            var swayRoll = (deterministicHeld ? Mathf.Cos(time) : Mathf.Cos(time * 2.55f + motionSeed * .73f)) * heldSwayRollDegrees * heldBlend;
            var swayYaw = (deterministicHeld ? Mathf.Sin(time) : Mathf.Sin(time * 1.85f + motionSeed * .41f)) * 4.5f * heldBlend;
            Pose(pelvis, new Vector3(swayPitch * .35f, swayYaw * .28f, swayRoll * .42f));
            Pose(spine01, new Vector3(swayPitch * .55f, swayYaw * .5f, swayRoll * .62f));
            Pose(spine02, new Vector3(swayPitch * .35f, swayYaw * .22f, swayRoll * .34f));
            if (pelvis.IsValid) pelvis.bone.localPosition = pelvis.bindPosition + Vector3.up * (Mathf.Abs(Mathf.Sin(walkPhase)) * .028f * walkBlend);
            CurrentWalkBoneAngle = Mathf.Abs(stride);
            CurrentHeldVisualAngle = Mathf.Sqrt(swayPitch * swayPitch + swayRoll * swayRoll);
        }

        private void RestoreAll()
        {
            Restore(pelvis); Restore(spine01); Restore(spine02); Restore(leftThigh); Restore(rightThigh);
            Restore(leftCalf); Restore(rightCalf); Restore(leftFoot); Restore(rightFoot); Restore(leftUpperArm);
            Restore(rightUpperArm); Restore(leftForearm); Restore(rightForearm);
        }

        private static void Restore(BonePose pose)
        {
            if (!pose.IsValid) return;
            pose.bone.localRotation = pose.bindRotation;
            pose.bone.localPosition = pose.bindPosition;
        }

        private static void Pose(BonePose pose, Vector3 localEuler)
        {
            if (pose.IsValid) pose.bone.localRotation = pose.bindRotation * Quaternion.Euler(localEuler);
        }

        private static Transform FindDeepChild(Transform parent, string childName)
        {
            if (parent == null) return null;
            foreach (var item in parent.GetComponentsInChildren<Transform>(true))
                if (string.Equals(item.name, childName, StringComparison.OrdinalIgnoreCase)) return item;
            return null;
        }
    }
}
