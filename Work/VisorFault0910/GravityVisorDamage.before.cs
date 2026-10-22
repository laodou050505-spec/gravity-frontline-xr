using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    /// <summary>Head-bound glass damage; live readouts and the aim cursor stay above it.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class GravityVisorDamage : MonoBehaviour
    {
        private RawImage overlay;
        private Material material;
        private GravityHud hud;
        private RectTransform layer;
        private float previousShield=100f;
        private bool initialized;
        public bool Ready => overlay != null && CrackTexture != null && material != null;
        public float DamageRatio { get; private set; }
        public float VisibleDamage { get; private set; }
        public float HitPulse { get; private set; }
        public float CriticalRatio => Mathf.Clamp01((VisibleDamage-.65f)/.35f);
        // Bounded attenuation keeps a critically injured player's surroundings readable.
        public float ScreenDarkening => .46f * Mathf.Pow(VisibleDamage, 1.16f);
        public float EdgeDarkening => .12f * Mathf.Pow(VisibleDamage, 1.6f);
        public float CentralCrackOpacity => .32f * Mathf.Pow(Mathf.Clamp01((VisibleDamage-.18f)/.67f), 1.2f);
        public RectTransform OverlayRect => overlay != null ? overlay.rectTransform : null;
        public bool Visible => overlay != null && overlay.gameObject.activeInHierarchy;
        public Material OverlayMaterial => material;
        public Texture2D CrackTexture { get; private set; }

        public void Configure(GravityHud owner)
        {
            hud=owner;
            CrackTexture=Resources.Load<Texture2D>("GravityUI/VisorGlassCracks");
            var template=Resources.Load<Material>("GravityUI/VisorDamage");
            layer=owner.transform.Find("VISOR_TOPMOST_CANVAS") as RectTransform;
            if (CrackTexture == null || template == null || layer == null) return;
            if(material==null) material=new Material(template) { name="VisorDamage_Runtime", renderQueue=4990 };
            var node=layer.Find("VISOR_GLASS_DAMAGE");
            if(node==null) { node=new GameObject("VISOR_GLASS_DAMAGE",typeof(RectTransform),typeof(RawImage)).transform; node.SetParent(layer,false); }
            overlay=node.GetComponent<RawImage>();
            overlay.texture=CrackTexture; overlay.material=material; overlay.raycastTarget=false;
            overlay.color=Color.white;
            var rect=overlay.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero; rect.sizeDelta=owner.ActiveReferenceResolution;
            rect.localScale=Vector3.one; rect.localRotation=Quaternion.identity;
            node.SetAsFirstSibling();
            FitOverlayToView();
            Apply();
        }
        public void SetHealth(float shield)
        {
            shield=Mathf.Clamp(shield,0,100);
            if(initialized && shield<previousShield-.01f)
            {
                HitPulse=Mathf.Clamp01(.35f+(previousShield-shield)/30f);
                Debug.Log($"GRAVITY_VISOR_DAMAGE_HIT shield={shield:0} missing={1-shield/100f:0.00} pulse={HitPulse:0.00}");
            }
            DamageRatio=1-shield/100f;
            previousShield=shield; initialized=true;
            Apply();
        }
        public void SetVisible(bool visible) { if(overlay!=null)overlay.gameObject.SetActive(visible); }
        private void Update()
        {
            if(!Ready)return;
            var rate=DamageRatio>VisibleDamage?2.8f:1.15f;
            VisibleDamage=Mathf.MoveTowards(VisibleDamage,DamageRatio,Time.unscaledDeltaTime*rate);
            HitPulse=Mathf.MoveTowards(HitPulse,0,Time.unscaledDeltaTime*2.5f);
            Apply();
        }
        // The HUD intentionally occupies only its safe reading area. Damage alone must
        // cover the whole eye: derive its rectangle from the live stereo projections,
        // without resizing the HUD or shifting its readouts.
        private void LateUpdate() { if (Ready && Visible) FitOverlayToView(); }
        private void FitOverlayToView()
        {
            if (overlay == null || layer == null || hud == null) return;
            var canvas=hud.GetComponent<Canvas>();
            var camera=canvas != null && canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (camera == null) return;
            var minimum=new Vector2(float.PositiveInfinity,float.PositiveInfinity);
            var maximum=new Vector2(float.NegativeInfinity,float.NegativeInfinity);
            if (camera.stereoEnabled)
            {
                if (!IncludeEye(camera,Camera.MonoOrStereoscopicEye.Left,ref minimum,ref maximum)
                    || !IncludeEye(camera,Camera.MonoOrStereoscopicEye.Right,ref minimum,ref maximum)) return;
            }
            else if (!IncludeEye(camera,Camera.MonoOrStereoscopicEye.Mono,ref minimum,ref maximum)) return;
            var rect=overlay.rectTransform;
            var size=(maximum-minimum)*1.06f;
            var centre=(minimum+maximum)*.5f;
            if (Vector2.SqrMagnitude(rect.sizeDelta-size)>.01f) rect.sizeDelta=size;
            if (Vector2.SqrMagnitude(rect.anchoredPosition-centre)>.01f) rect.anchoredPosition=centre;
        }
        private bool IncludeEye(Camera camera,Camera.MonoOrStereoscopicEye eye,ref Vector2 minimum,ref Vector2 maximum)
        {
            var plane=new Plane(layer.forward,layer.position);
            for (int corner=0;corner<4;corner++)
            {
                var ray=camera.ViewportPointToRay(new Vector3(corner&1,(corner>>1)&1,0),eye);
                if (!plane.Raycast(ray,out var distance) || distance<=0) return false;
                var point=layer.InverseTransformPoint(ray.GetPoint(distance));
                minimum=Vector2.Min(minimum,new Vector2(point.x,point.y));
                maximum=Vector2.Max(maximum,new Vector2(point.x,point.y));
            }
            return true;
        }
        public bool CoversViewport(Camera camera,out string detail)
        {
            detail="overlay_or_camera_missing";
            if (overlay == null || camera == null) return false;
            var rect=overlay.rectTransform;
            var plane=new Plane(rect.forward,rect.position);
            int eyes=camera.stereoEnabled?2:1;
            for (int eyeIndex=0;eyeIndex<eyes;eyeIndex++)
            {
                var eye=eyes==1?Camera.MonoOrStereoscopicEye.Mono
                    :eyeIndex==0?Camera.MonoOrStereoscopicEye.Left:Camera.MonoOrStereoscopicEye.Right;
                for (int corner=0;corner<4;corner++)
                {
                    var ray=camera.ViewportPointToRay(new Vector3(corner&1,(corner>>1)&1,0),eye);
                    if (!plane.Raycast(ray,out var distance) || distance<=0) { detail="view_ray_missed_overlay"; return false; }
                    var point=rect.InverseTransformPoint(ray.GetPoint(distance));
                    if (!rect.rect.Contains(new Vector2(point.x,point.y))) { detail=$"uncovered_eye={eye} corner={corner}"; return false; }
                }
            }
            detail=$"eyes={eyes} coveredCorners={eyes*4} size={rect.sizeDelta} margin=6%";
            return true;
        }
        private void Apply()
        {
            if(material==null)return;
            material.SetFloat("_Damage",VisibleDamage);
            material.SetFloat("_HitPulse",HitPulse);
            material.SetFloat("_Darkening",ScreenDarkening);
            material.SetFloat("_EdgeDarkening",EdgeDarkening);
            material.SetFloat("_CentralCracks",CentralCrackOpacity);
        }
        private void OnDestroy() { if(material!=null)Destroy(material); }
    }
}
