using System;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace GravityRepulsion
{
    /// <summary>
    /// Owns the isolated title phase.  The gameplay scene is not loaded while this object is
    /// alive: START performs a single-scene transition, which destroys this TitleRoot entirely.
    /// </summary>
    public sealed class GravityTitleController : MonoBehaviour
    {
        public const string GameplaySceneName = "GravityBoulevard";

        public GravityDesignStart designStart;
        public string gameplaySceneName = GameplaySceneName;

        private bool startWasPressed;
        private bool recenterWasPressed;
        private bool loading;
        private float titleReadyAt;
        public bool IsReadyForButtonInput => !loading && Time.unscaledTime >= titleReadyAt;

        private void Awake()
        {
            designStart ??= GetComponent<GravityDesignStart>();
            designStart?.ApplyToRig();
            // Some desktop/XR runtimes retain a button state while the player is launching.
            // Arm transitions only after that state has first been released; a held startup
            // trigger must never skip the independently reviewable title phase.
            startWasPressed = ReadStartPressed();
            recenterWasPressed = ReadRecenterPressed();
            titleReadyAt = Time.unscaledTime + 2f;
            Debug.Log($"GRAVITY_TITLE_PHASE_READY root={designStart?.worldPosition} yaw={designStart?.worldYaw:0.0} eye={designStart?.eyeHeight:0.000} gameplayLoaded=false");
            if (Array.IndexOf(Environment.GetCommandLineArgs(), "-gravitySmoke") >= 0) { Application.runInBackground = true; Invoke(nameof(StartGame), 2.1f); }
        }

        private void Update()
        {
            // The menu owns hit-tested activation. Global trigger input must not
            // start gameplay while the player is aiming at EXIT or empty space.

            var recenterPressed = ReadRecenterPressed();
            if (recenterPressed && !recenterWasPressed) RecenterTitle();
            recenterWasPressed = recenterPressed;
        }

        public void StartGame()
        {
            if (loading) return;
            loading = true;
            Debug.Log("GRAVITY_TITLE_START_PRESSED loading=GravityBoulevard mode=Single titleRootWillBeDestroyed=true");
            SceneManager.LoadSceneAsync(gameplaySceneName, LoadSceneMode.Single);
        }

        public void RecenterTitle()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PXR_Plugin.Sensor.UPxr_ResetSensor(ResetSensorOption.ResetAll);
#endif
            designStart?.ApplyToRig();
            Debug.Log("GRAVITY_TITLE_RECENTER_PASS currentPhase=Title gameplayLoaded=false");
            FindFirstObjectByType<GravityTitleHud>()?.ReframeAtCurrentView();
        }

        public void ExitGame()
        {
            Debug.Log("GRAVITY_TITLE_EXIT_PRESSED");
            Application.Quit();
        }

        private void OnDestroy()
        {
            if (loading)
                Debug.Log("GRAVITY_TITLE_ROOT_DESTROYED gameplayTransition=true gameplayWasNotLoadedDuringTitle=true");
        }

        private static bool ReadStartPressed()
        {
            if (Keyboard.current != null && (Keyboard.current.enterKey.isPressed || Keyboard.current.spaceKey.isPressed)) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            return ReadHandButton(UnityEngine.XR.CommonUsages.triggerButton) || ReadHandButton(UnityEngine.XR.CommonUsages.primaryButton);
#else
            return false;
#endif
        }

        private static bool ReadRecenterPressed()
        {
            if (Keyboard.current != null && Keyboard.current.cKey.isPressed) return true;
#if UNITY_ANDROID && !UNITY_EDITOR
            var left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
            return left.isValid && left.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out var pressed) && pressed;
#else
            return false;
#endif
        }

        private static bool ReadHandButton(InputFeatureUsage<bool> usage)
        {
            foreach (var node in new[] { XRNode.LeftHand, XRNode.RightHand })
            {
                var device = InputDevices.GetDeviceAtXRNode(node);
                if (device.isValid && device.TryGetFeatureValue(usage, out var pressed) && pressed) return true;
            }
            return false;
        }
    }
}
