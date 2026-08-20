using UnityEngine;

namespace GravityRepulsion
{
    [ExecuteAlways]
    public sealed class GravityNightSky : MonoBehaviour
    {
        public Transform playerHead;
        public Transform moon;
        public Renderer stars;

        private Material runtimeStarMaterial;

        private void OnEnable()
        {
            ConfigureTwinkleMaterial();
            RefreshNow(Camera.main != null ? Camera.main.transform : playerHead);
        }

        private void OnDisable()
        {
            if (runtimeStarMaterial == null) return;
            if (Application.isPlaying) Destroy(runtimeStarMaterial);
            else DestroyImmediate(runtimeStarMaterial);
            runtimeStarMaterial = null;
        }

        private void LateUpdate()
        {
            RefreshNow(Camera.main != null ? Camera.main.transform : playerHead);
        }

        public void RefreshNow(Transform viewer)
        {
            playerHead = viewer != null ? viewer : playerHead;
            if (playerHead == null) return;
            transform.position = playerHead.position;
            if (moon != null)
            {
                moon.LookAt(playerHead.position, Vector3.up);
                moon.Rotate(0f, 180f, 0f, Space.Self);
            }
        }

        private void ConfigureTwinkleMaterial()
        {
            if (!Application.isPlaying || stars == null || runtimeStarMaterial != null) return;
            var shader = Resources.Load<Shader>("Shaders/GravityStarTwinkle");
            if (shader == null) return;
            var source = stars.sharedMaterial;
            runtimeStarMaterial = source != null ? new Material(source) : new Material(shader);
            runtimeStarMaterial.shader = shader;
            runtimeStarMaterial.name = "M_RuntimeStarTwinkle_单Renderer随机相位";
            if (source != null && source.mainTexture != null) runtimeStarMaterial.SetTexture("_MainTex", source.mainTexture);
            if (source != null) runtimeStarMaterial.SetColor("_Color", source.color);
            runtimeStarMaterial.SetFloat("_TwinkleStrength", .18f);
            runtimeStarMaterial.SetFloat("_TwinkleSpeed", .62f);
            stars.sharedMaterial = runtimeStarMaterial;
            Debug.Log("GRAVITY_STAR_TWINKLE_PASS rendererCount=1 perStarUpdate=false randomizedShaderPhase=true strength=0.18 speed=0.62");
        }
    }
}
