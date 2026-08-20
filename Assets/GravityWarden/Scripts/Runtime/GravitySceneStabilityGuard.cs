using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GravityRepulsion
{
    public sealed class GravitySceneStabilityGuard : MonoBehaviour
    {
        public Transform environmentRoot;
        public float observationDuration = 5f;
        public bool Completed { get; private set; }
        public bool Passed { get; private set; }
        public string FailureSummary { get; private set; }

        private readonly struct Pose
        {
            public readonly Vector3 position;
            public readonly Quaternion rotation;
            public Pose(Transform item) { position = item.position; rotation = item.rotation; }
        }

        private IEnumerator Start()
        {
            yield return RunCheck();
        }

        private IEnumerator RunCheck()
        {
            environmentRoot ??= GameObject.Find("ENVIRONMENT_欧式钟楼大道_可替换模型")?.transform;
            if (environmentRoot == null)
            {
                Fail("environment root missing");
                yield break;
            }

            var fixedItems = environmentRoot.GetComponentsInChildren<Transform>(true)
                .Where(t => t != environmentRoot && t.GetComponentInParent<GravityTarget>() == null).ToArray();
            var initial = fixedItems.ToDictionary(t => t, t => new Pose(t));
            var dynamicProps = FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)
                .Where(t => t.kind == GravityTargetKind.Prop && t.IsAlive).ToArray();
            var dynamicStart = dynamicProps.ToDictionary(t => t, t => t.transform.position);
            var violations = new List<string>();
            var maxFixedMove = 0f;
            var maxFixedAngle = 0f;
            var maxIdleSpeed = 0f;
            var maxIdleAngular = 0f;

            for (var elapsed = 0f; elapsed < observationDuration; elapsed += Time.deltaTime)
            {
                foreach (var pair in initial)
                {
                    if (pair.Key == null) continue;
                    maxFixedMove = Mathf.Max(maxFixedMove, Vector3.Distance(pair.Value.position, pair.Key.position));
                    maxFixedAngle = Mathf.Max(maxFixedAngle, Quaternion.Angle(pair.Value.rotation, pair.Key.rotation));
                }
                foreach (var prop in dynamicProps)
                {
                    if (prop == null || !prop.IsAlive || prop.IsHeld || prop.Body == null) continue;
                    maxIdleSpeed = Mathf.Max(maxIdleSpeed, prop.Body.linearVelocity.magnitude);
                    maxIdleAngular = Mathf.Max(maxIdleAngular, prop.Body.angularVelocity.magnitude);
                    if (prop.Body.linearVelocity.magnitude > 6f || prop.Body.angularVelocity.magnitude > 12f)
                        violations.Add($"{prop.name}: speed={prop.Body.linearVelocity.magnitude:0.00} angular={prop.Body.angularVelocity.magnitude:0.00}");
                }
                yield return null;
            }

            foreach (var pair in initial)
            {
                if (pair.Key == null) continue;
                var move = Vector3.Distance(pair.Value.position, pair.Key.position);
                var angle = Quaternion.Angle(pair.Value.rotation, pair.Key.rotation);
                if (move > .001f || angle > .05f) violations.Add($"{pair.Key.name}: fixedMove={move:0.0000} fixedAngle={angle:0.000}");
            }
            foreach (var pair in dynamicStart)
            {
                if (pair.Key == null || !pair.Key.IsAlive || pair.Key.IsHeld) continue;
                var move = Vector3.Distance(pair.Value, pair.Key.transform.position);
                if (move > 1.25f) violations.Add($"{pair.Key.name}: idleDisplacement={move:0.000}");
            }

            if (violations.Count > 0)
            {
                Fail(string.Join(" | ", violations.Distinct().Take(12)));
                yield break;
            }
            Completed = true;
            Passed = true;
            Debug.Log($"GRAVITY_SCENE_STABILITY_PASS duration={observationDuration:0.0}s fixed={fixedItems.Length} dynamicProps={dynamicProps.Length} maxFixedMove={maxFixedMove:0.0000} maxFixedAngle={maxFixedAngle:0.000} maxIdleSpeed={maxIdleSpeed:0.00} maxIdleAngular={maxIdleAngular:0.00}");
        }

        private void Fail(string message)
        {
            Completed = true;
            Passed = false;
            FailureSummary = message;
            Debug.LogError("GRAVITY_SCENE_STABILITY_FAIL " + message);
        }
    }
}
