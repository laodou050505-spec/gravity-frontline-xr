using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace GravityRepulsion
{
    /// <summary>Head-relative optical HUD. Static lettering/optics are Tripo art; all values are live.</summary>
    [DefaultExecutionOrder(200)]
    public sealed class GravityVisorHud : MonoBehaviour
    {
        public const int SortOrder = 32000;
        private GravityHud hud;
        private RectTransform layer;
        private Material opticalMaterial, topMaterial;
        private Texture2D optics, labels;
        private RawImage leftState, rightState, notice, energyRing;
        private RectTransform sweep, reticle;
        private Text bearing;
        private GravityGameManager currentGame;
        private GravityHand aimHand;
        private Image[] lockCorners;
        private Image aimDot;
        private GravityHand loggedAimHand;
        private GravityTarget loggedAimTarget;
        private Vector2 loggedAimPosition;
        private float nextAimTraceTime;
        public GravityHand AimHand => aimHand;
        public RectTransform AimReticle => reticle;
        public bool AimLocked { get; private set; }
        private Image[] shieldSegments, repulseSegments;
        private readonly List<RectTransform> radarContacts = new();
        private float noticeUntil;
        private static readonly Color Cyan = new(.27f, .91f, 1f, .92f);
        private static readonly Color Violet = new(.68f, .42f, 1f, .92f);
        private static readonly Color Amber = new(1f, .68f, .29f, .95f);
        public bool Ready => layer != null && optics != null && labels != null && topMaterial != null;
        public bool TopmostConfigured => Ready && GetComponent<Canvas>().sortingOrder == SortOrder;
        private bool Pico => GravityHud.UsesPicoLayout;
        private float SideX => Pico ? 720f : 580f;
        private float TopY => Pico ? 800f : 395f;
        private float BottomY => Pico ? -700f : -355f;
        private float SideY => Pico ? 310f : 180f;
        private float FontScale => Pico ? 1.32f : 1f;

        public void Configure(GravityHud owner)
        {
            hud = owner;
            optics = Resources.Load<Texture2D>("GravityUI/JarvisVisorOverlay");
            labels = Resources.Load<Texture2D>("GravityUI/VisorLabels");
            opticalMaterial = Resources.Load<Material>("GravityUI/VisorOverlay");
            topMaterial = Resources.Load<Material>("GravityUI/Topmost");
            if (optics == null || labels == null || opticalMaterial == null || topMaterial == null) return;
            // No nested renderMode: Unity applies it to the root canvas too.
            // Reuse the serialized layer on every configure, preventing duplicate HUDs.
            layer = transform.Find("VISOR_TOPMOST_CANVAS") as RectTransform;
            if (layer == null) layer = new GameObject("VISOR_TOPMOST_CANVAS", typeof(RectTransform)).GetComponent<RectTransform>();
            layer.SetParent(transform, false);
            var obsoleteCanvas = layer.GetComponent<Canvas>();
            if (obsoleteCanvas != null) { if (Application.isPlaying) Destroy(obsoleteCanvas); else DestroyImmediate(obsoleteCanvas); }
            Place(layer, Vector2.zero, owner.ActiveReferenceResolution);
            layer.gameObject.SetActive(true);
            // Serialized scenes can still contain the previous large optical layer.
            foreach (var name in new[] { "VISOR_NANOBANANA_OPTICAL_LAYER", "AIM_OPTIC", "VISOR_TOP_READOUT", "VISOR_BOTTOM_READOUT", "VISOR_LEFT_TELEMETRY", "VISOR_RIGHT_TELEMETRY", "VISOR_LEFT_SCANLINE", "VISOR_RIGHT_SCANLINE", "VISOR_TOP_SCANLINE", "VISOR_BOTTOM_SCANLINE", "AIM_LOCK_LABEL" })
            {
                var old = layer.Find(name);
                if (old != null) { old.gameObject.SetActive(false); if (Application.isPlaying) Destroy(old.gameObject); else DestroyImmediate(old.gameObject); }
            }
            var canvas = owner.GetComponent<Canvas>(); canvas.overrideSorting = true; canvas.sortingOrder = SortOrder;

            float sx = SideX, top = TopY, bottom = BottomY, side = SideY;
            // Optical perimeter: only the generated corner fragments, never a large circle over the action.
            float edgeX = Pico ? 1010 : 735, edgeY = Pico ? 960 : 455;
            for (int i = 0; i < 4; i++)
            {
                bool right = (i & 1) != 0, lower = (i & 2) != 0;
                var image = Art("OPTICAL_EDGE_" + i, optics, new Vector2(right ? edgeX-170 : -edgeX+170, lower ? -edgeY+130 : edgeY-130), new Vector2(340,260));
                image.uvRect = new Rect(right ? .65f : 0, lower ? 0 : .65f, .35f,.35f);
                image.color = new Color(1,1,1,.55f);
            }
            Stroke("VISOR_BROW", new(-sx-100,top+65), new(-300,top+65), Cyan*.7f);
            Stroke("VISOR_BROW_R", new(300,top+65), new(sx+100,top+65), Cyan*.7f);
            Caption("SHIELD_LABEL", 4, new(0,top+52), 96);
            Value(hud.shieldText, new(0,top+14), new(145,48), 35, Cyan);
            shieldSegments = Segments("SHIELD_SEG", 24, new(0,top-25), 470, 8, Cyan);
            Caption("BRAND", 7, new(0,top+100), 230);
            Caption("THREAT", 0, new(-sx,side+122), 185);
            Caption("WAVE_LABEL", 1, new(-sx-62,side+80), 65);
            Value(hud.waveText, new(-sx+73,side+80), new(100,42), 26, Cyan);
            Caption("ENEMIES_LABEL", 2, new(-sx-36,side+36), 115);
            Value(hud.enemyText, new(-sx+78,side+36), new(96,42), 26, Cyan);
            Stroke("THREAT_RULE", new(-sx-110,side+10), new(-sx+110,side+10), Cyan*.55f);

            var radar = Ring("THREAT_RADAR", new(-sx,side-130), 180, .50f);
            sweep = Stroke("RADAR_SWEEP", new(-sx,side-130), new(-sx+77,side-130), Cyan*.65f).rectTransform;
            sweep.pivot = new Vector2(0,.5f); sweep.anchoredPosition = new(-sx,side-130);
            radarContacts.Clear();
            for (int i=0; i<16; i++) radarContacts.Add(Block("RADAR_CONTACT_"+i,new(-sx,side-130),new(5,5),Amber).rectTransform);

            Caption("COMBAT_LABEL", 3, new(sx,side+122), 205);
            Value(hud.scoreText, new(sx,side+73), new(250,55), 34, Cyan);
            Caption("ENERGY_LABEL", 5, new(sx,side+5), 155);
            energyRing = Ring("SINGULARITY_RING",new(sx,side-130),195,.65f);
            Value(hud.energyText,new(sx,side-130),new(200,50),25,Violet);

            Caption("REPULSE_LABEL", 6, new(-sx,bottom+30),140);
            Value(hud.repulseText,new(-sx,bottom-18),new(160,52),31,Cyan);
            repulseSegments=Segments("REPULSE_SEG",12,new(-sx,bottom-62),235,6,Violet);
            // Preserve generated gauntlet captions and true input legends from the incumbent art.
            Slice("LEFT_GAUNTLET", "HudBottomLeft", new(-sx,bottom+84),new(255,45),new(.06f,.72f,.88f,.16f));
            Slice("RIGHT_GAUNTLET", "HudBottomRight", new(sx,bottom+84),new(255,45),new(.06f,.72f,.88f,.16f));
            Slice("LEFT_INPUT_LEGEND", "HudBottomLeft",new(-sx,bottom-123),new(240,67),new(.05f,.02f,.90f,.20f));
            Slice("RIGHT_INPUT_LEGEND", "HudBottomRight",new(sx,bottom-123),new(240,67),new(.05f,.02f,.90f,.20f));
            leftState=Art("LEFT_STATE",Load("Idle"),new(-sx+138,bottom-18),new(95,40));
            rightState=Art("RIGHT_STATE",Load("Idle"),new(sx,bottom-18),new(150,58));
            Stroke("CHIN_L",new(-sx+145,bottom-94),new(-250,bottom-94),Cyan*.55f);
            Stroke("CHIN_R",new(250,bottom-94),new(sx-145,bottom-94),Violet*.55f);
            Caption("LOWER_BRAND",7,new(0,bottom-94),200);
            // Minimal lock brackets leave the central aiming area readable. The whole group
            // is repositioned from the active hand's real aim ray every frame below.
            reticle=CreateLockReticle();
            notice=Art("VISOR_STATUS",Load("Ready"),new(0,-100),new(138,52)); notice.gameObject.SetActive(false);
            bearing=NewValue("HEADING",new(0,top-75),new(150,32),17,Cyan*.8f);
            for(int i=-5;i<=5;i++) Block("HEADING_TICK_"+i,new(i*38,top-68),new(2,i==0?14:7),Cyan*.5f);
            HideLegacy(); ApplyTopmostMaterials();
            Debug.Log("GRAVITY_VISOR_HUD_READY layout=visor_perimeter shield=top radar=live core=right gauntlets=lower fixedText=Tripo_NanoBanana depth=Always queue=4990");
        }

        public void Refresh(GravityGameManager game)
        {
            if (!Ready || game == null) return;
            currentGame = game;
            hud.waveText.text=game.Wave.ToString("00"); hud.enemyText.text=game.AliveEnemies.Count.ToString("00");
            hud.scoreText.text=game.Score.ToString("00000"); hud.shieldText.text=Mathf.CeilToInt(game.Shield).ToString("000");
            hud.energyText.text=$"{Mathf.FloorToInt(game.UltimateCharge)}/{game.UltimateRequirement}";
            hud.repulseText.text=game.RepulseCooldown.ToString("0.0");
            leftState.texture=Load(State(game.leftHand)); rightState.texture=Load(State(game.rightHand));
            float shield=Mathf.Clamp01(game.Shield/100f), energy=Mathf.Clamp01(game.UltimateCharge/Mathf.Max(1,game.UltimateRequirement));
            Fill(shieldSegments,shield,shield<=.3f?Amber:Cyan);
            Fill(repulseSegments,1-game.RepulseCooldown/Mathf.Max(.01f,game.repulseCooldownDuration),Violet);
            energyRing.color=energy>=1 ? Amber : new Color(.65f,.50f,1,.35f+energy*.6f);
            RefreshAim(game);
            sweep.localRotation=Quaternion.Euler(0,0,-Time.unscaledTime*40);
            bearing.text=$"{Mathf.RoundToInt(game.playerHead.eulerAngles.y)%360:000}°";
            for(int i=0;i<radarContacts.Count;i++)
            {
                bool visible=i<game.AliveEnemies.Count && game.AliveEnemies[i]!=null;
                radarContacts[i].gameObject.SetActive(visible);
                if(!visible) continue;
                var delta=Quaternion.Inverse(Quaternion.Euler(0,game.playerHead.eulerAngles.y,0))*(game.AliveEnemies[i].transform.position-game.playerHead.position);
                var plot=Vector2.ClampMagnitude(new Vector2(delta.x,delta.z)*3,70);
                radarContacts[i].anchoredPosition=new Vector2(-SideX,SideY-130)+plot;
            }
            if(Time.unscaledTime>=noticeUntil) notice.gameObject.SetActive(false);
        }
        public void Status(HudStatusEvent ev)
        {
            if(!Ready)return;
            string texName = ev switch {
                HudStatusEvent.Danger  => "Danger",
                HudStatusEvent.Holding => "Holding",
                HudStatusEvent.Locked  => "Locked",
                HudStatusEvent.Impact  => "Impact",
                _                      => "Ready",
            };
            notice.texture=Load(texName);
            notice.gameObject.SetActive(true); noticeUntil=Time.unscaledTime+1.65f;
        }
        public void SetVisible(bool visible) { if(layer!=null)layer.gameObject.SetActive(visible); }
        private static string State(GravityHand h)=>h!=null&&h.Held!=null?"Holding":h!=null&&h.Candidate!=null?"Locked":"Idle";
        private void LateUpdate()
        {
            if (Ready && currentGame != null && layer.gameObject.activeInHierarchy) RefreshAim(currentGame);
        }

        public void RefreshAim(GravityGameManager game)
        {
            if (!Ready || reticle == null || game == null) return;
            var left = game.leftHand;
            var right = game.rightHand;
            if (aimHand == null || (aimHand != left && aimHand != right)) aimHand = right ?? left;
            // Explicit interaction wins briefly; otherwise the last deliberate hand motion
            // selects the cursor. Merely hovering a target never steals the other hand's aim.
            var grabHand = left != null && (right == null || left.LastGrabInputTime > right.LastGrabInputTime) ? left : right;
            if (grabHand != null && Time.unscaledTime - grabHand.LastGrabInputTime < .75f) aimHand = grabHand;
            else
            {
                var movingHand = left != null && (right == null || left.LastAimInputTime > right.LastAimInputTime) ? left : right;
                if (movingHand != null && (aimHand == null || movingHand.LastAimInputTime > aimHand.LastAimInputTime + .08f)) aimHand = movingHand;
            }
            if (aimHand == null) { reticle.gameObject.SetActive(false); AimLocked = false; return; }
            var target = aimHand.Held != null ? aimHand.Held : aimHand.Candidate;
            AimLocked = target != null && target.IsAlive;
            var color = AimLocked ? new Color(.4f,1f,.75f,.96f) : new Color(.6f,.87f,1f,.85f);
            aimDot.color = color;
            foreach (var corner in lockCorners) { corner.color = color; corner.gameObject.SetActive(AimLocked); }
            MoveReticleToPoint(aimHand.AimPoint);
            if (Application.isPlaying && Time.unscaledTime >= nextAimTraceTime &&
                (loggedAimHand != aimHand || loggedAimTarget != target || Vector2.Distance(loggedAimPosition, reticle.anchoredPosition) > 24f))
            {
                nextAimTraceTime = Time.unscaledTime + .5f;
                loggedAimHand = aimHand; loggedAimTarget = target; loggedAimPosition = reticle.anchoredPosition;
                Debug.Log($"GRAVITY_AIM_TRACK side={aimHand.side} locked={AimLocked} target={target?.name ?? "none"} visible={reticle.gameObject.activeSelf} local={reticle.anchoredPosition} ray={aimHand.AimRay.direction}");
            }
        }

        private void MoveReticleToPoint(Vector3 worldPoint)
        {
            var canvas = hud.GetComponent<Canvas>();
            var camera = canvas.worldCamera != null ? canvas.worldCamera : Camera.main;
            if (camera == null) { reticle.gameObject.SetActive(false); return; }
            var viewport = camera.WorldToViewportPoint(worldPoint);
            // Hide offscreen/behind-eye aim; clamping would falsely suggest an edge target.
            if (viewport.z <= 0 || viewport.x < 0 || viewport.x > 1 || viewport.y < 0 || viewport.y > 1)
            { reticle.gameObject.SetActive(false); return; }
            var eyeRay = new Ray(camera.transform.position, worldPoint-camera.transform.position);
            var plane = new Plane(layer.forward, layer.position);
            if (!plane.Raycast(eyeRay, out var distance) || distance <= 0)
            { reticle.gameObject.SetActive(false); return; }
            var local = layer.InverseTransformPoint(eyeRay.GetPoint(distance));
            var position = new Vector2(local.x, local.y);
            reticle.gameObject.SetActive(layer.rect.Contains(position));
            reticle.anchoredPosition = position;
        }

        private RectTransform CreateLockReticle()
        {
            var group = Element<RectTransform>("AIM_LOCK_RETICLE");
            Place(group, Vector2.zero, new Vector2(64,64));
            var c = new Color(.6f,.87f,1f,.85f);
            float outer = Pico ? 32f : 24f, inner = outer*.55f;
            LineLocal(group,"L_TOP",new(-outer,outer),new(-inner,outer),c); LineLocal(group,"L_TOP_R",new(inner,outer),new(outer,outer),c);
            LineLocal(group,"L_BOTTOM",new(-outer,-outer),new(-inner,-outer),c); LineLocal(group,"L_BOTTOM_R",new(inner,-outer),new(outer,-outer),c);
            LineLocal(group,"L_LEFT",new(-outer,outer),new(-outer,inner),c); LineLocal(group,"L_LEFT_B",new(-outer,-inner),new(-outer,-outer),c);
            LineLocal(group,"L_RIGHT",new(outer,outer),new(outer,inner),c); LineLocal(group,"L_RIGHT_B",new(outer,-inner),new(outer,-outer),c);
            lockCorners = group.GetComponentsInChildren<Image>(true);
            aimDot = Element<Image>("AIM_LOCK_DOT"); aimDot.transform.SetParent(group,false);
            Place(aimDot.rectTransform,Vector2.zero,new Vector2(Pico?6:4,Pico?6:4));
            aimDot.color=c; aimDot.material=topMaterial; aimDot.raycastTarget=false;
            foreach (var corner in lockCorners) if (corner != aimDot) corner.gameObject.SetActive(false);
            // Reconfigure also reuses child corners and dot, with no stale label or duplicate.
            lockCorners = System.Array.FindAll(group.GetComponentsInChildren<Image>(true), image => image != aimDot);
            var oldLabel=group.Find("AIM_LOCK_LABEL");
            if(oldLabel!=null) { oldLabel.gameObject.SetActive(false); if(Application.isPlaying) Destroy(oldLabel.gameObject); else DestroyImmediate(oldLabel.gameObject); }
            group.SetAsLastSibling(); return group;
        }

        private void LineLocal(RectTransform parent,string name,Vector2 from,Vector2 to,Color color)
        {
            var line=Element<Image>(name); line.transform.SetParent(parent,false); Place(line.rectTransform,(from+to)/2,new(Vector2.Distance(from,to),3));
            line.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg); line.color=color; line.material=topMaterial; line.raycastTarget=false;
        }
        private static Texture2D Load(string name)=>Resources.Load<Texture2D>("GravityUI/"+name);
        private void HideLegacy()
        {
            foreach(var name in new[]{"GENERATED_TOP_LEFT","GENERATED_TOP_RIGHT","GENERATED_BOTTOM_LEFT","GENERATED_BOTTOM_RIGHT","GENERATED_STATUS","HUD_FRAME_TOP_LEFT","HUD_FRAME_TOP_RIGHT","HUD_FRAME_BOTTOM_LEFT","HUD_FRAME_BOTTOM_RIGHT"})
            { var old=transform.Find(name); if(old!=null)old.gameObject.SetActive(false); }
            foreach(var label in new[]{hud.leftText,hud.rightText,hud.statusText})if(label!=null)label.gameObject.SetActive(false);
            foreach(var fill in new[]{hud.shieldFill,hud.energyFill,hud.repulseFill})if(fill!=null)fill.transform.parent.gameObject.SetActive(false);
        }
        public void ApplyTopmostMaterials()
        {
            if(topMaterial==null)return;
            foreach(var graphic in GetComponentsInChildren<Graphic>(true))
                if(graphic is Text || graphic is Image || (graphic is RawImage && graphic.material==Graphic.defaultGraphicMaterial))graphic.material=topMaterial;
        }
        public bool Validate(out string failure)
        {
            failure="";
            if(!TopmostConfigured)failure+="visor_not_topmost;";
            foreach(var graphic in GetComponentsInChildren<Graphic>())
                if(graphic.material==null || graphic.material.renderQueue<4990)failure+="non_overlay_material:"+graphic.name+";";
            return failure.Length==0;
        }
        private static void Fill(Image[] images,float ratio,Color color)
        { for(int i=0;i<images.Length;i++)images[i].color=i<Mathf.CeilToInt(Mathf.Clamp01(ratio)*images.Length)?color:new Color(.06f,.14f,.18f,.85f); }
        private Image[] Segments(string name,int count,Vector2 centre,float width,float height,Color color)
        {
            var result=new Image[count]; float step=width/count;
            for(int i=0;i<count;i++)result[i]=Block(name+i,centre+new Vector2(-width*.5f+step*(i+.5f),0),new(step-3,height),color);
            return result;
        }
        private RawImage Ring(string name,Vector2 position,float size,float alpha)
        { var image=Art(name,optics,position,new(size,size));image.uvRect=new Rect(.205f,.205f,.59f,.59f);image.color=new Color(1,1,1,alpha);return image; }
        private RawImage Slice(string name,string resource,Vector2 pos,Vector2 size,Rect uv)
        { var image=Art(name,Load(resource),pos,size);image.uvRect=uv;return image; }
        private void Caption(string name,int row,Vector2 pos,float width)
        {
            // Tight individual row bounds measured from the generated eight-line atlas (1024 square).
            int[] widths={456,206,318,548,240,435,306,660};
            int[] tops={110,214,316,417,521,624,726,827};
            float w=widths[row];
            var image=Art(name,labels,pos,new(width,width*80/w));
            image.uvRect=new Rect((512-w/2)/1024f,1-(tops[row]+80)/1024f,w/1024f,80/1024f);
        }
        private RawImage Art(string name,Texture2D texture,Vector2 position,Vector2 size)
        {
            var item=Element<RawImage>(name);item.texture=texture;item.material=opticalMaterial;item.color=Color.white;item.raycastTarget=false;
            Place(item.rectTransform,position,size); return item;
        }
        private Image Block(string name,Vector2 position,Vector2 size,Color color)
        { var image=Element<Image>(name);Place(image.rectTransform,position,size);image.color=color;image.material=topMaterial;image.raycastTarget=false;return image; }
        private Image Stroke(string name,Vector2 from,Vector2 to,Color color)
        { var image=Block(name,(from+to)/2,new(Vector2.Distance(from,to),2),color);image.rectTransform.localRotation=Quaternion.Euler(0,0,Mathf.Atan2(to.y-from.y,to.x-from.x)*Mathf.Rad2Deg);return image; }
        private Text NewValue(string name,Vector2 pos,Vector2 size,int fontSize,Color color)
        { var value=Element<Text>(name);Value(value,pos,size,fontSize,color);return value; }
        private void Value(Text text,Vector2 position,Vector2 size,int fontSize,Color color)
        {
            text.transform.SetParent(layer,false);Place(text.rectTransform,position,size*FontScale);
            text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.fontSize=Mathf.RoundToInt(fontSize*FontScale);text.fontStyle=FontStyle.Bold;
            text.color=color;text.material=topMaterial;text.alignment=TextAnchor.MiddleCenter;text.raycastTarget=false;text.enabled=true;text.gameObject.SetActive(true);
            text.horizontalOverflow=HorizontalWrapMode.Overflow;text.verticalOverflow=VerticalWrapMode.Overflow;
            var outline=text.GetComponent<Outline>()??text.gameObject.AddComponent<Outline>();outline.effectColor=new Color(.005f,.012f,.025f,.95f);outline.effectDistance=new Vector2(1.5f,-1.5f);
        }
        private T Element<T>(string name) where T:Component
        {
            Transform child = null;
            foreach (var candidate in layer.GetComponentsInChildren<RectTransform>(true))
                if (candidate.name == name) { child = candidate; break; }
            if (child == null)
            {
                child = new GameObject(name, typeof(RectTransform)).transform;
                child.SetParent(layer,false);
            }
            child.gameObject.SetActive(true);
            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }
        private static void Place(RectTransform r,Vector2 position,Vector2 size)
        {r.anchorMin=r.anchorMax=r.pivot=new(.5f,.5f);r.anchoredPosition=position;r.sizeDelta=size;r.localScale=Vector3.one;r.localRotation=Quaternion.identity;}
    }
}
