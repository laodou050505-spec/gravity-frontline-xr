using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRepulsion
{
    public static class GravityFeedback
    {
        private sealed class FeedbackRunner : MonoBehaviour { }
        private sealed class FieldFollower : MonoBehaviour
        {
            public GravityTarget target;
            public GravityHand hand;
            public LineRenderer beam;
            public Transform ringA;
            public Transform ringB;

            private void LateUpdate()
            {
                if (target == null || hand == null || !target.IsAlive || target.HeldBy != hand)
                {
                    Destroy(gameObject);
                    return;
                }
                var center = target.Center;
                beam.SetPosition(0, hand.RayOriginPosition);
                beam.SetPosition(1, center);
                transform.position = center;
                var size = Mathf.Clamp(target.WorldBounds.extents.magnitude * .62f, .28f, 1.15f);
                ringA.localScale = Vector3.one * size;
                ringB.localScale = Vector3.one * size * 1.12f;
                ringA.Rotate(0f, 28f * Time.deltaTime, 16f * Time.deltaTime, Space.Self);
                ringB.Rotate(20f * Time.deltaTime, -24f * Time.deltaTime, 0f, Space.Self);
                var pulse = 1f + Mathf.Sin(Time.time * 2.4f) * .018f;
                ringA.localScale *= pulse;
            }
        }

        private static FeedbackRunner runner;
        private static Material violetAdditive;
        private static Material cyanAdditive;
        private static Material warmAdditive;
        private static readonly List<Material> RuntimeMaterials = new();

        public static void AttachGravityField(GravityTarget target, GravityHand hand)
        {
            EnsureRunner();
            var old = target.GetComponentInChildren<FieldFollower>();
            if (old != null) Object.Destroy(old.gameObject);
            var root = new GameObject("VFX_引力束_挣扎环");
            root.transform.SetParent(target.transform, true);
            var follower = root.AddComponent<FieldFollower>();
            follower.target = target;
            follower.hand = hand;
            follower.beam = root.AddComponent<LineRenderer>();
            follower.beam.useWorldSpace = true;
            follower.beam.positionCount = 2;
            follower.beam.widthCurve = new AnimationCurve(new Keyframe(0f, .006f), new Keyframe(.62f, .021f), new Keyframe(1f, .004f));
            follower.beam.numCapVertices = 4;
            follower.beam.material = GetMaterial(new Color(.42f, .48f, .72f), false);
            follower.beam.startColor = new Color(.43f, .68f, .86f, .24f);
            follower.beam.endColor = new Color(.58f, .42f, .76f, .34f);
            follower.ringA = CreateRing("引力环_A", root.transform, new Color(.49f, .43f, .7f), .011f, .24f);
            follower.ringB = CreateRing("引力环_B", root.transform, new Color(.38f, .59f, .74f), .007f, .16f);
        }

        public static void Burst(Vector3 position, Color color, int count = 20, float speed = 1f)
        {
            EnsureRunner();
            count = Mathf.Min(count, Application.isMobilePlatform ? 42 : 72);
            for (var i = 0; i < count; i++)
            {
                var spark = GameObject.CreatePrimitive(i % 4 == 0 ? PrimitiveType.Cube : PrimitiveType.Sphere);
                spark.name = "VFX_GravityShard";
                Object.Destroy(spark.GetComponent<Collider>());
                spark.transform.position = position;
                spark.transform.localScale = Vector3.one * Random.Range(.025f, .075f);
                spark.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, false);
                runner.StartCoroutine(AnimateSpark(spark, Random.onUnitSphere * Random.Range(1.5f, 5f) * speed, Random.onUnitSphere * 360f));
            }
        }

        public static void Shockwave(Vector3 position, Color color, float maxScale = 5f)
        {
            EnsureRunner();
            var ring = CreateRing("VFX_斥力冲击环", null, color, .045f).gameObject;
            ring.transform.position = position;
            ring.transform.rotation = RingFacingViewer(position);
            ring.transform.localScale = Vector3.one * .2f;
            runner.StartCoroutine(AnimateRing(ring, Mathf.Min(maxScale, 3.6f), .42f));
            var echo = CreateRing("VFX_斥力回响", null, new Color(.18f, .75f, 1f), .018f).gameObject;
            echo.transform.position = position;
            echo.transform.rotation = RingFacingViewer(position);
            echo.transform.localScale = Vector3.one * .15f;
            runner.StartCoroutine(AnimateRing(echo, Mathf.Min(maxScale * .34f, 1.55f), .48f));
        }

        public static void TargetPulse(Vector3 position, Color color, float maxScale)
        {
            EnsureRunner();
            for (var i = 0; i < 2; i++)
            {
                var ring = CreateRing("VFX_目标锁定环", null, color, i == 0 ? .028f : .014f).gameObject;
                ring.transform.position = position;
                ring.transform.rotation = Quaternion.Euler(i * 90f, i * 45f, 0f);
                ring.transform.localScale = Vector3.one * .12f;
                runner.StartCoroutine(AnimateRing(ring, maxScale * (1f + i * .22f), .35f + i * .12f));
            }
        }

        public static void HitFlash(Vector3 position, Color color)
        {
            EnsureRunner();
            // Primary flash sphere
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "VFX_受击闪光";
            Object.Destroy(flash.GetComponent<Collider>());
            flash.transform.position = position;
            flash.transform.localScale = Vector3.one * .10f;
            flash.GetComponent<Renderer>().sharedMaterial = GetMaterial(color, false);
            runner.StartCoroutine(AnimateFlash(flash, 1.05f, .18f));
            // Secondary soft glow ring for depth
            var ring = CreateRing("VFX_受击光环", null, color, .022f, .55f).gameObject;
            ring.transform.position = position;
            ring.transform.rotation = RingFacingViewer(position);
            ring.transform.localScale = Vector3.one * .08f;
            runner.StartCoroutine(AnimateRing(ring, .72f, .22f));
        }

        public static void BlockBreak(Vector3 position, float radius)
        {
            HitFlash(position, Color.white);
            Shockwave(position, new Color(.62f, .28f, 1f), Mathf.Clamp(radius * 2f, 1.3f, 3.2f));
            Burst(position, new Color(.38f, .74f, 1f), 28, 1.3f);
        }

        public static void EnvironmentImpact(Vector3 position, float strength)
        {
            HitFlash(position, new Color(1f, .62f, .22f));
            Burst(position, new Color(.35f, .72f, 1f), Mathf.RoundToInt(Mathf.Clamp(strength * 1.2f, 8f, 22f)), .7f);
            Shockwave(position, new Color(.5f, .26f, 1f), Mathf.Clamp(strength * .17f, 1f, 2.8f));
        }

        public static GameObject CreateSingularity(Vector3 position)
        {
            EnsureRunner();
            var root = new GameObject("VFX_地爆天星_四阶段奇点");
            root.transform.position = position;
            var core = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            core.name = "暗紫奇点核心";
            Object.Destroy(core.GetComponent<Collider>());
            core.transform.SetParent(root.transform, false);
            core.transform.localScale = Vector3.one * .5f;
            core.GetComponent<Renderer>().sharedMaterial = GetMaterial(new Color(.32f, .06f, .68f), false);
            for (var i = 0; i < 3; i++)
            {
                var ring = CreateRing("轨道能量环_" + i, root.transform, i == 1 ? new Color(.16f, .7f, 1f) : new Color(.7f, .22f, 1f), .02f + i * .006f);
                ring.localScale = Vector3.one * (1.1f + i * .45f);
                ring.rotation = Quaternion.Euler(i * 57f, i * 36f, i * 23f);
                ring.gameObject.AddComponent<GravitySpin>().axis = new Vector3(70f + i * 20f, 120f - i * 18f, 45f);
            }
            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(.55f, .22f, 1f);
            light.range = 13f;
            light.intensity = 3.2f;
            return root;
        }

        private sealed class GravitySpin : MonoBehaviour
        {
            public Vector3 axis;
            private void Update() => transform.Rotate(axis * Time.deltaTime, Space.Self);
        }

        private static Transform CreateRing(string name, Transform parent, Color color, float width, float alpha = .82f)
        {
            const int segments = 48;
            var go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            var line = go.AddComponent<LineRenderer>();
            line.loop = true;
            line.useWorldSpace = false;
            line.positionCount = segments;
            line.widthMultiplier = width;
            line.numCapVertices = 3;
            line.sharedMaterial = GetMaterial(color, false);
            line.startColor = line.endColor = new Color(color.r, color.g, color.b, alpha);
            for (var i = 0; i < segments; i++)
            {
                var a = i * Mathf.PI * 2f / segments;
                line.SetPosition(i, new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)));
            }
            return go.transform;
        }

        private static Quaternion RingFacingViewer(Vector3 position)
        {
            var camera = Camera.main;
            var normal = camera != null ? camera.transform.position - position : Vector3.forward;
            if (normal.sqrMagnitude < .001f) normal = Vector3.forward;
            return Quaternion.FromToRotation(Vector3.up, normal.normalized);
        }

        private static Material GetMaterial(Color color, bool warm)
        {
            EnsureRunner();
            if (warm && warmAdditive != null) return warmAdditive;
            if (!warm && color.b > color.r * 1.3f && cyanAdditive != null) return cyanAdditive;
            if (!warm && color.r >= color.b * .68f && violetAdditive != null) return violetAdditive;
            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Unlit/Color") ?? Shader.Find("Standard");
            var material = new Material(shader) { color = color };
            if (material.HasProperty("_EmissionColor")) material.SetColor("_EmissionColor", color * 4f);
            material.renderQueue = 3100;
            RuntimeMaterials.Add(material);
            if (warm) warmAdditive = material;
            else if (color.b > color.r * 1.3f) cyanAdditive = material;
            else violetAdditive = material;
            return material;
        }

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("GravityFeedbackRunner_对象池与特效");
            Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<FeedbackRunner>();
        }

        private static IEnumerator AnimateSpark(GameObject spark, Vector3 velocity, Vector3 spin)
        {
            var life = .62f;
            while (spark != null && life > 0f)
            {
                life -= Time.deltaTime;
                velocity += Physics.gravity * (.22f * Time.deltaTime);
                spark.transform.position += velocity * Time.deltaTime;
                spark.transform.Rotate(spin * Time.deltaTime, Space.Self);
                spark.transform.localScale *= Mathf.Pow(.025f, Time.deltaTime);
                yield return null;
            }
            if (spark != null) Object.Destroy(spark);
        }

        private static IEnumerator AnimateRing(GameObject ring, float maxScale, float duration)
        {
            var time = 0f;
            while (ring != null && time < duration)
            {
                time += Time.deltaTime;
                var p = Mathf.Clamp01(time / duration);
                var scale = Mathf.Lerp(.2f, maxScale, 1f - Mathf.Pow(1f - p, 3f));
                ring.transform.localScale = Vector3.one * scale;
                var line = ring.GetComponent<LineRenderer>();
                if (line != null)
                {
                    var c = line.startColor;
                    c.a = (1f - p) * .86f;
                    line.startColor = line.endColor = c;
                }
                yield return null;
            }
            if (ring != null) Object.Destroy(ring);
        }

        private static IEnumerator AnimateFlash(GameObject flash, float scale, float duration)
        {
            var time = 0f;
            while (flash != null && time < duration)
            {
                time += Time.deltaTime;
                var p = Mathf.Clamp01(time / duration);
                flash.transform.localScale = Vector3.one * Mathf.Lerp(.08f, scale, Mathf.Sin(p * Mathf.PI));
                yield return null;
            }
            if (flash != null) Object.Destroy(flash);
        }
    }
}
