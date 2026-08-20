using System.Collections;
using UnityEngine;

namespace GravityRepulsion
{
    public sealed class RobotBreakApartEffect : MonoBehaviour
    {
        public Transform visualRoot;
        public int maxFragments = 30;
        public float fragmentLifetime = 1.45f;
        public float explosionForce = 7.5f;
        public static float LastCenterYDrift { get; private set; }
        public static bool LastCenterDissolvePassed { get; private set; }
        private bool exploded;

        public void Explode()
        {
            if (exploded) return;
            exploded = true;
            if (!isActiveAndEnabled || !gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }
            var target = GetComponent<GravityTarget>();
            var center = target != null ? target.Center : transform.position + Vector3.up;
            var root = visualRoot != null ? visualRoot : transform;
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            LastCenterYDrift = 0f;
            LastCenterDissolvePassed = false;
            GravityFeedback.Burst(center, new Color(.38f, .56f, .92f), 34, .9f);
            GravityFeedback.TargetPulse(center, new Color(.5f, .42f, .78f), 1.35f);
            GravityAudio.Play(GravitySound.Kill, center, 1f);
            var anchor = new GameObject("DISSOLVE_CENTER_几何中心固定锚").transform;
            anchor.SetParent(transform, true);
            anchor.position = center;
            anchor.rotation = root.rotation;
            root.SetParent(anchor, true);
            StartCoroutine(ControlledDissolve(anchor, renderers, center));
        }

        private IEnumerator ControlledDissolve(Transform anchor, Renderer[] renderers, Vector3 fixedCenter)
        {
            var startScale = anchor.localScale;
            var property = new MaterialPropertyBlock();
            const float flashDuration = .12f;
            for (var time = 0f; time < flashDuration; time += Time.deltaTime)
            {
                var flash = 1f - Mathf.Clamp01(time / flashDuration);
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(property);
                    property.SetColor("_EmissionColor", new Color(.45f, .82f, 1f) * Mathf.Lerp(2f, 7f, flash));
                    renderer.SetPropertyBlock(property);
                }
                RecordCenterDrift(renderers, fixedCenter);
                yield return null;
            }

            var duration = Mathf.Clamp(fragmentLifetime * .32f, .42f, .55f);
            for (var time = 0f; time < duration && anchor != null; time += Time.deltaTime)
            {
                var p = Mathf.Clamp01(time / duration);
                var flash = Mathf.Sin(p * Mathf.PI);
                anchor.localScale = startScale * Mathf.Lerp(1f + flash * .045f, .02f, p * p);
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    renderer.GetPropertyBlock(property);
                    property.SetColor("_EmissionColor", Color.Lerp(new Color(.3f, .82f, 1f) * 5f, new Color(.45f, .08f, 1f), p));
                    renderer.SetPropertyBlock(property);
                }
                RecordCenterDrift(renderers, fixedCenter);
                yield return null;
            }
            foreach (var renderer in renderers) if (renderer != null) renderer.enabled = false;
            foreach (var collider in GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            LastCenterDissolvePassed = LastCenterYDrift < .03f;
            if (LastCenterDissolvePassed)
                Debug.Log($"GRAVITY_DEATH_CENTER_PASS object={name} maxCenterYDrift={LastCenterYDrift:0.0000}m destroyed=true");
            else
                Debug.LogError($"GRAVITY_DEATH_CENTER_FAIL object={name} maxCenterYDrift={LastCenterYDrift:0.0000}m");
            Destroy(gameObject);
        }

        private static void RecordCenterDrift(Renderer[] renderers, Vector3 fixedCenter)
        {
            var found = false;
            var bounds = new Bounds(fixedCenter, Vector3.zero);
            foreach (var renderer in renderers)
            {
                if (renderer == null || !renderer.enabled) continue;
                if (!found) { bounds = renderer.bounds; found = true; }
                else bounds.Encapsulate(renderer.bounds);
            }
            if (found) LastCenterYDrift = Mathf.Max(LastCenterYDrift, Mathf.Abs(bounds.center.y - fixedCenter.y));
        }
    }
}
