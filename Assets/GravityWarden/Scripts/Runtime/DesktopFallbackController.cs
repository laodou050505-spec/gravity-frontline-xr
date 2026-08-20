using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;

namespace GravityRepulsion
{
    public sealed class DesktopFallbackController : MonoBehaviour
    {
        public Transform rigRoot;
        public Camera viewCamera;
        public GravityDesignStart designStart;
        public float lookSensitivity = .11f;
        public float moveSpeed = 3.6f;

        private float yaw;
        private float pitch;
        private Quaternion initialRigRotation;

        private void Start()
        {
            viewCamera ??= Camera.main;
            rigRoot ??= transform.root;
            designStart ??= GetComponent<GravityDesignStart>();
            if (rigRoot != null) initialRigRotation = rigRoot.rotation;
            if (viewCamera != null)
            {
                yaw = viewCamera.transform.eulerAngles.y;
                pitch = viewCamera.transform.eulerAngles.x;
            }
        }

        private void Update()
        {
            if (XRSettings.isDeviceActive || viewCamera == null) return;
            var keyboard = Keyboard.current;
            var mouse = Mouse.current;
            if (keyboard == null) return;
            if (mouse != null && mouse.rightButton.isPressed)
            {
                var delta = mouse.delta.ReadValue();
                yaw += delta.x * lookSensitivity;
                pitch = Mathf.Clamp(pitch - delta.y * lookSensitivity, -38f, 38f);
                viewCamera.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
            }
            var move = Vector3.zero;
            if (keyboard.wKey.isPressed) move += viewCamera.transform.forward;
            if (keyboard.sKey.isPressed) move -= viewCamera.transform.forward;
            if (keyboard.dKey.isPressed) move += viewCamera.transform.right;
            if (keyboard.aKey.isPressed) move -= viewCamera.transform.right;
            move.y = 0f;
            if (move.sqrMagnitude > 1f) move.Normalize();
            rigRoot.position += move * (moveSpeed * Time.deltaTime);
            if (keyboard.cKey.wasPressedThisFrame)
            {
                if (designStart != null)
                {
                    designStart.ApplyToRig();
                    initialRigRotation = designStart.WorldRotation;
                }
                else
                {
                    rigRoot.position = new Vector3(0f, 0f, 10.5f);
                    rigRoot.rotation = initialRigRotation;
                }
                yaw = viewCamera.transform.eulerAngles.y;
                pitch = 0f;
                viewCamera.transform.localRotation = Quaternion.identity;
            }
        }
    }
}
