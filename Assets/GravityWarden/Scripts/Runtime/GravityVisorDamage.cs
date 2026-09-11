using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    /// <summary>Head-bound glass damage; live readouts and the aim cursor stay above it.</summary>
    [DefaultExecutionOrder(220)]
    public sealed class GravityVisorDamage : MonoBehaviour
    {
        private RawImage overlay, faultOverlay, centralOverlay;
        private Material material, faultMaterial, centerMaterial;
        private GravityHud hud;
        private RectTransform layer;
        private float previousShield=100f;
        private bool initialized;
        private float heldFaultTime;
        public bool DeathStateHeld { get; private set; }
        public bool Ready => overlay != null && faultOverlay != null && centralOverlay != null
            && CrackTexture != null && CenterTexture != null && material != null && faultMaterial != null && centerMaterial != null;
        public float DamageRatio { get; private set; }
        public float VisibleDamage { get; private set; }
        public float HitPulse { get; private set; }
        public float CriticalRatio => Mathf.Clamp01((VisibleDamage-.65f)/.35f);
        // Bounded attenuation keeps a critically injured player's surroundings readable.
        public float ScreenDarkening => .82f * Mathf.Pow(VisibleDamage, .75f);
        public float EdgeDarkening => .12f * VisibleDamage;
        public float FaultRatio => Mathf.SmoothStep(0f,1f,Mathf.Clamp01((VisibleDamage-.18f)/.82f));
        public float CentralCrackOpacity => .82f * Mathf.SmoothStep(0f,1f,Mathf.Clamp01((VisibleDamage-.18f)/.67f));
        public float EdgeCrackStrength => .25f + .75f * Mathf.SmoothStep(0f,1f,VisibleDamage);
        public RectTransform OverlayRect => faultOverlay != null ? faultOverlay.rectTransform : null;
        public bool Visible => overlay != null && overlay.gameObject.activeInHierarchy;
        public Material OverlayMaterial => material;
        public Texture2D CrackTexture { get; private set; }
        public Texture2D CenterTexture { get; private set; }
        public Material FaultMaterial => faultMaterial;
        public Material CenterMaterial => centerMaterial;

        public void Configure(GravityHud owner)
        {
            hud=owner;
            CrackTexture=Resources.Load<Texture2D>("GravityUI/VisorGlassCracks");
            CenterTexture=Resources.Load<Texture2D>("GravityUI/VisorCentralCracks");
            var template=Resources.Load<Material>("GravityUI/VisorDamage");
            var supplement=Resources.Load<Material>("GravityUI/VisorSupplement");
            var readouts=owner.transform.Find("VISOR_TOPMOST_CANVAS");
            if(CrackTexture==null || CenterTexture==null || template==null || supplement==null || readouts==null)return;
            // The display damage survives when combat telemetry is hidden for results.
            layer=owner.transform.Find("VISOR_DAMAGE_CANVAS") as RectTransform;
            if(layer==null)layer=new GameObject("VISOR_DAMAGE_CANVAS",typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(owner.transform,false);
            layer.anchorMin=layer.anchorMax=layer.pivot=new Vector2(.5f,.5f);
            layer.anchoredPosition=Vector2.zero;layer.sizeDelta=owner.ActiveReferenceResolution;
            layer.localScale=Vector3.one;layer.localRotation=Quaternion.identity;
            layer.gameObject.SetActive(true);
            layer.SetAsFirstSibling();
            foreach(var name in new[]{"VISOR_DISPLAY_FAULT","VISOR_GLASS_DAMAGE","VISOR_CENTRAL_CRACKS"})
            {
                var existing=readouts.Find(name);
                if(existing!=null)existing.SetParent(layer,false);
            }
            if(material==null)material=new Material(template) { name="VisorGlass_Original",renderQueue=4990 };
            if(faultMaterial==null)faultMaterial=new Material(supplement) { name="VisorDisplay_Fault",renderQueue=4990 };
            if(centerMaterial==null)centerMaterial=new Material(supplement) { name="VisorGlass_Central",renderQueue=4990 };
            faultMaterial.SetFloat("_Layer",0f); centerMaterial.SetFloat("_Layer",1f);
            // Keep the accepted glass at its original reference size and UVs. Only
            // the separate display-fault layer follows the full stereo field of view.
            faultOverlay=MakeLayer("VISOR_DISPLAY_FAULT",Texture2D.whiteTexture,faultMaterial);
            overlay=MakeLayer("VISOR_GLASS_DAMAGE",CrackTexture,material);
            centralOverlay=MakeLayer("VISOR_CENTRAL_CRACKS",CenterTexture,centerMaterial);
            // A 16:10 image must not be stretched into the square PICO reading area.
            var reference=owner.ActiveReferenceResolution;
            float centralScale=Mathf.Min(reference.x/CenterTexture.width,reference.y/CenterTexture.height);
            centralOverlay.rectTransform.sizeDelta=new Vector2(CenterTexture.width,CenterTexture.height)*centralScale;
            faultOverlay.transform.SetAsFirstSibling();
            overlay.transform.SetSiblingIndex(1); centralOverlay.transform.SetSiblingIndex(2);
            FitOverlayToView();
            Apply();
        }
        private RawImage MakeLayer(string name,Texture texture,Material effect)
        {
            var node=layer.Find(name);
            if(node==null){node=new GameObject(name,typeof(RectTransform),typeof(RawImage)).transform;node.SetParent(layer,false);}
            var image=node.GetComponent<RawImage>();
            image.texture=texture;image.material=effect;image.raycastTarget=false;image.color=Color.white;
            image.uvRect=new Rect(0,0,1,1);
            var rect=image.rectTransform;
            rect.anchorMin=rect.anchorMax=rect.pivot=new Vector2(.5f,.5f);
            rect.anchoredPosition=Vector2.zero;rect.sizeDelta=hud.ActiveReferenceResolution;
            rect.localScale=Vector3.one;rect.localRotation=Quaternion.identity;
            return image;
        }
        public void SetHealth(float shield)
        {
            if(DeathStateHeld)return;
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
        public void SetVisible(bool visible)
        {
            if(overlay!=null)overlay.gameObject.SetActive(visible);
            if(faultOverlay!=null)faultOverlay.gameObject.SetActive(visible);
            if(centralOverlay!=null)centralOverlay.gameObject.SetActive(visible);
        }
        public void HoldDeathState()
        {
            if(DeathStateHeld)return;
            // Commit the lethal hit before the same-frame summary appears, even when
            // that hit went straight from a healthy visor to zero health.
            VisibleDamage=DamageRatio;
            heldFaultTime=Time.unscaledTime;
            DeathStateHeld=true;
            SetVisible(true);
            Apply();
            Debug.Log($"GRAVITY_VISOR_DEATH_HELD damage={VisibleDamage:0.000} darkening={ScreenDarkening:0.000} faultTime={heldFaultTime:0.000}");
        }
        public void ResetForCombat()
        {
            DeathStateHeld=false;
            initialized=false;previousShield=100f;
            DamageRatio=VisibleDamage=HitPulse=0f;
            heldFaultTime=0f;
            SetVisible(true);
            Apply();
        }
        private void Update()
        {
            if(!Ready || DeathStateHeld)return;
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
            if (faultOverlay == null || layer == null || hud == null) return;
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
            var rect=faultOverlay.rectTransform;
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
            if (faultOverlay == null || camera == null) return false;
            var rect=faultOverlay.rectTransform;
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
            material.SetFloat("_EdgeStrength",EdgeCrackStrength);
            material.SetFloat("_HitPulse",HitPulse);
            if(faultMaterial!=null)
            {
                faultMaterial.SetFloat("_Darkening",ScreenDarkening);
                faultMaterial.SetFloat("_FaultRatio",FaultRatio);
                faultMaterial.SetFloat("_HitPulse",HitPulse);
                faultMaterial.SetFloat("_FaultTime",DeathStateHeld?heldFaultTime:Time.unscaledTime);
            }
            if(centerMaterial!=null)centerMaterial.SetFloat("_CentralCracks",CentralCrackOpacity);
        }
        private void OnDestroy() { if(material!=null)Destroy(material);if(faultMaterial!=null)Destroy(faultMaterial);if(centerMaterial!=null)Destroy(centerMaterial); }
    }
}
