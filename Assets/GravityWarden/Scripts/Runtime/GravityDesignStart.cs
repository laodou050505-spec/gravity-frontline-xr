using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

namespace GravityRepulsion
{
    /// <summary>
    /// The single authored player start for both the desktop camera and the PICO XR rig.
    /// Positions are measured against the street's battle-axis landmark, not against UI.
    /// </summary>
    public sealed class GravityDesignStart : MonoBehaviour
    {
        [Header("Battle-axis start: street landmark at world origin")]
        public Vector3 worldPosition = new(0f, 0f, 10.5f);
        [Range(-180f, 180f)] public float worldYaw = 180f;
        [Min(.5f)] public float eyeHeight = 2.336f;
        [Min(0f)] public float trackedViewLift;
        public Transform gameplayLandmark;

        [Header("Physical world-space mission board, in front of the start")]
        // Kept off the battle axis: a walk-up command terminal, never a screen in front of play.
        public Vector3 missionBoardOffset = new(-3.35f, 1.65f, 3.7f);

        public Quaternion WorldRotation => Quaternion.Euler(0f, worldYaw, 0f);
        public Vector3 WorldEyePosition => worldPosition + Vector3.up * eyeHeight;
        public Vector3 MissionBoardPosition => worldPosition + WorldRotation * missionBoardOffset;
        public Quaternion MissionBoardRotation
        {
            get
            {
                var towardPlayer = Vector3.ProjectOnPlane(WorldEyePosition - MissionBoardPosition, Vector3.up);
                return towardPlayer.sqrMagnitude < .001f ? Quaternion.identity : Quaternion.LookRotation(towardPlayer.normalized, Vector3.up);
            }
        }

        private bool liftReported;
        private void Awake() => ApplyToRig();
        private void LateUpdate() => ApplyTrackedViewLift();
        private void ApplyTrackedViewLift()
        {
            if (!XRSettings.isDeviceActive || trackedViewLift <= 0f) return;
            var origin = GetComponent<XROrigin>();
            if (origin == null || origin.CameraFloorOffsetObject == null) return;
            var offset = origin.CameraFloorOffsetObject.transform;
            var position = offset.localPosition; position.y = trackedViewLift; offset.localPosition = position;
            if (!liftReported && Time.timeSinceLevelLoad > 2f && origin.Camera != null)
            {
                liftReported = true;
                Debug.Log($"GRAVITY_VIEW_HEIGHT_RUNTIME trackedEye={origin.Camera.transform.localPosition.y:0.000} lift={offset.localPosition.y:0.000} worldEye={origin.Camera.transform.position.y:0.000}");
            }
        }

        public void ApplyToRig()
        {
            transform.SetPositionAndRotation(worldPosition, WorldRotation);
            ApplyTrackedViewLift();
            Debug.Log($"GRAVITY_DESIGN_START_APPLIED landmark={gameplayLandmark?.name ?? "StreetBattleAxis"} root={worldPosition} yaw={worldYaw:0.0} eye={eyeHeight:0.000}");
        }

        public bool MatchesRig(float tolerance = .01f)
        {
            return Vector3.Distance(transform.position, worldPosition) <= tolerance
                   && Mathf.Abs(Mathf.DeltaAngle(transform.eulerAngles.y, worldYaw)) <= .1f;
        }
    }
}
