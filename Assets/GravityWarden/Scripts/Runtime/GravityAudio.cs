using System;
using System.Collections.Generic;
using UnityEngine;

namespace GravityRepulsion
{
    public enum GravitySound
    {
        Select, PullStage, Grab, Release, Repulse, Projectile, Reflect, Hit, Kill,
        Pickup, ShieldBreak, EnvironmentImpact, UltimateCharge, UltimateCompress, UltimateBlast, UiReady,
        PlayerHurt, Suction
    }

    public static class GravityAudio
    {
        private sealed class AudioRunner : MonoBehaviour { }
        private static AudioRunner runner;
        private static readonly Dictionary<GravitySound, AudioClip> Clips = new();

        public static void Play(GravitySound sound, Vector3 position, float volume = 1f, float pitch = 1f)
        {
            EnsureRunner();
            var source = new GameObject("SFX_" + sound).AddComponent<AudioSource>();
            source.transform.position = position;
            source.clip = GetClip(sound);
            source.volume = Mathf.Clamp01(volume) * .72f;
            source.pitch = pitch * UnityEngine.Random.Range(.96f, 1.04f);
            source.spatialBlend = .92f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = .55f;
            source.maxDistance = 24f;
            source.dopplerLevel = sound == GravitySound.Projectile ? .65f : .05f;
            source.Play();
            UnityEngine.Object.Destroy(source.gameObject, source.clip.length / Mathf.Max(.1f, source.pitch) + .12f);
        }

        private static void EnsureRunner()
        {
            if (runner != null) return;
            var go = new GameObject("GravityAudioRunner_原创程序化空间音效");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<AudioRunner>();
        }

        private static AudioClip GetClip(GravitySound sound)
        {
            if (Clips.TryGetValue(sound, out var existing)) return existing;
            var parameters = sound switch
            {
                GravitySound.Select => (.10f, 760f, 1160f, .08f, .02f),
                GravitySound.PullStage => (.22f, 190f, 620f, .18f, .08f),
                GravitySound.Grab => (.24f, 150f, 540f, .14f, .03f),
                GravitySound.Release => (.15f, 620f, 170f, .11f, .04f),
                GravitySound.Repulse => (.52f, 90f, 420f, .24f, .28f),
                GravitySound.Projectile => (.36f, 180f, 320f, .12f, .18f),
                GravitySound.Reflect => (.25f, 240f, 880f, .13f, .08f),
                GravitySound.Hit => (.16f, 110f, 230f, .07f, .52f),
                GravitySound.Kill => (.48f, 240f, 70f, .18f, .42f),
                GravitySound.Pickup => (.42f, 420f, 920f, .24f, .03f),
                GravitySound.ShieldBreak => (.44f, 320f, 80f, .16f, .5f),
                GravitySound.EnvironmentImpact => (.24f, 120f, 75f, .08f, .58f),
                GravitySound.UltimateCharge => (.9f, 70f, 410f, .52f, .11f),
                GravitySound.UltimateCompress => (.62f, 360f, 80f, .33f, .2f),
                GravitySound.UltimateBlast => (1.05f, 54f, 140f, .18f, .56f),
                GravitySound.PlayerHurt => (.30f, 380f, 105f, .015f, .46f),
                GravitySound.Suction => (.78f, 72f, 760f, .035f, .38f),
                _ => (.18f, 520f, 760f, .1f, .04f)
            };
            var clip = Synthesize(sound.ToString(), parameters.Item1, parameters.Item2, parameters.Item3, parameters.Item4, parameters.Item5);
            Clips[sound] = clip;
            return clip;
        }

        private static AudioClip Synthesize(string name, float duration, float startHz, float endHz, float attack, float noise)
        {
            const int rate = 24000;
            var samples = Mathf.CeilToInt(duration * rate);
            var data = new float[samples];
            var phase = 0f;
            var random = new System.Random(name.GetHashCode());
            var suction = string.Equals(name, nameof(GravitySound.Suction), StringComparison.Ordinal);
            for (var i = 0; i < samples; i++)
            {
                var t = i / (float)rate;
                var p = i / (float)Mathf.Max(1, samples - 1);
                var frequency = Mathf.Lerp(startHz, endHz, p * p * (3f - 2f * p));
                phase += frequency * Mathf.PI * 2f / rate;
                var envelope = Mathf.Min(1f, t / Mathf.Max(.005f, attack)) * Mathf.Pow(1f - p, suction ? .72f : 1.55f);
                var tonal = Mathf.Sin(phase) * .58f + Mathf.Sin(phase * 2.01f) * .18f + Mathf.Sin(phase * .5f) * .12f;
                if (suction)
                {
                    var pressurePulse = .74f + Mathf.Sin(t * 2f * Mathf.PI * 8.5f) * .16f;
                    var lowRumble = Mathf.Sin(phase * .34f) * .34f;
                    var intakeWhistle = Mathf.Sin(phase * 2.7f + Mathf.Sin(t * 16f) * .7f) * .14f;
                    tonal = (tonal * .62f + lowRumble + intakeWhistle) * pressurePulse;
                }
                var randomSample = (float)(random.NextDouble() * 2.0 - 1.0);
                data[i] = Mathf.Clamp((tonal * (1f - noise) + randomSample * noise) * envelope, -.95f, .95f);
            }
            var clip = AudioClip.Create("Generated_" + name, samples, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
