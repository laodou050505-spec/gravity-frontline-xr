using UnityEngine;

namespace GravityRepulsion
{
    public static class GravityMusic
    {
        private const string ResourcePath = "Audio/BGM_ForTheBlackLordLoop";
        private static AudioSource source;

        public static bool IsConfigured => source != null && source.clip != null && source.loop && !source.spatialize && source.spatialBlend <= .001f;
        public static string ClipName => source != null && source.clip != null ? source.clip.name : string.Empty;

        public static void EnsurePlaying()
        {
            if (source != null)
            {
                if (!source.isPlaying) source.Play();
                return;
            }

            var clip = Resources.Load<AudioClip>(ResourcePath);
            if (clip == null)
            {
                Debug.LogError("GRAVITY_BGM_MISSING resource=" + ResourcePath);
                return;
            }

            var root = new GameObject("BGM_机械欧式街道战斗循环_CC-BY-4.0");
            Object.DontDestroyOnLoad(root);
            source = root.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = .24f;
            source.spatialBlend = 0f;
            source.spatialize = false;
            source.dopplerLevel = 0f;
            source.priority = 32;
            source.Play();
            Debug.Log($"GRAVITY_BGM_PASS clip={clip.name} loop=true volume={source.volume:0.00} spatial=false source=OpenGameArt_CC-BY-4.0");
        }
    }
}
