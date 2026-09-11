using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace GravityRepulsion
{
    public enum GravityTargetKind
    {
        Robot,
        Prop,
        Projectile,
        Heal
    }

    [RequireComponent(typeof(Rigidbody))]
    public sealed class GravityTarget : MonoBehaviour
    {
        public GravityTargetKind kind = GravityTargetKind.Prop;
        public string displayName = "可吸取物";
        public int maxHealth = 1;
        public int ultimateCharge;
        public bool canBlockOnce = true;
        public Transform replaceableVisual;
        [SerializeField] private Collider[] safeTriggerColliders = Array.Empty<Collider>();
        [Min(1)] public int pullStagesRequired = 1;
        public float heldFollowSharpness = 11f;
        public float heldStruggle = .035f;

        public bool IsAlive { get; private set; } = true;
        public bool IsHeld => HeldBy != null;
        public float HeldVisualScaleRatio => kind == GravityTargetKind.Robot && replaceableVisual != null && heldVisualScaleInitialized
            ? replaceableVisual.localScale.x / Mathf.Max(.0001f, heldVisualBaseScale.x)
            : 1f;
        public bool BlockAvailable { get; private set; }
        public GravityHand HeldBy { get; private set; }
        public Rigidbody Body { get; private set; }
        public int Health { get; private set; }
        public float ThrownUntil { get; private set; }
        public float RepulsedUntil { get; private set; }
        public int PullStagesCompleted { get; private set; }
        public Vector3 Center => WorldBounds.center;
        public bool IsScenePreview
        {
            get
            {
                for (var current = transform; current != null; current = current.parent)
                {
                    if (current.name.StartsWith("SCENE_PREVIEW_ENEMIES_", StringComparison.Ordinal)) return true;
                }
                return false;
            }
        }

        public static readonly string[] ApprovedScenePropDisplayNames =
        {
            "蒸汽消防栓", "机械邮筒", "街道隔离柱", "手压水泵", "街边金属长椅", "金属货箱", "齿轮装饰街道筒"
        };

        public bool HasValidVisibleInteractionGeometry(out string failure)
        {
            failure = string.Empty;
            var geometryRenderers = GetComponentsInChildren<Renderer>(true);
            var geometryColliders = GetComponentsInChildren<Collider>(true);
            var visible = geometryRenderers.Where(r => r != null && r.enabled && r.gameObject.activeInHierarchy && r.bounds.extents.sqrMagnitude > .0004f).ToArray();
            var solid = geometryColliders.Where(c => c != null && c.enabled && c.gameObject.activeInHierarchy && c.bounds.extents.sqrMagnitude > .0004f).ToArray();
            if (visible == null || visible.Length == 0) failure += "renderer_missing_or_empty;";
            if (solid == null || solid.Length == 0) failure += "collider_missing_or_empty;";
            if (GetComponentsInChildren<Transform>(true).Any(t => t != null && t.name.StartsWith("VISUAL_可吸道具_", StringComparison.Ordinal)))
                failure += "generated_interaction_visual_forbidden;";
            if (kind == GravityTargetKind.Prop && !MatchesApprovedPropIdentity()) failure += "prop_identity_mismatch;";
            return failure.Length == 0;
        }

        public bool MatchesApprovedPropIdentity()
        {
            if (kind != GravityTargetKind.Prop) return true;
            return !IsScenePreview && TryGetApprovedScenePropDisplayName(name, out var approvedName) && displayName == approvedName;
        }

        public static bool TryGetApprovedScenePropDisplayName(string objectName, out string approvedName)
        {
            approvedName = string.Empty;
            if (string.IsNullOrEmpty(objectName)) return false;
            if (objectName.StartsWith("tripo_convert_0a16797e-2372-4122-82ee-5a4b7a810995", StringComparison.Ordinal))
                approvedName = "蒸汽消防栓";
            else if (objectName.StartsWith("tripo_convert_45459f4d-1251-4a44-b5be-690fbc1b60b4", StringComparison.Ordinal))
                approvedName = "机械邮筒";
            else if (objectName.StartsWith("tripo_convert_9a369c7b-c9bd-4b04-9200-292a71135d35", StringComparison.Ordinal))
                approvedName = "街道隔离柱";
            else if (objectName.StartsWith("tripo_convert_5a9fa6f2-50fc-4988-84ca-c06bf4d9dbb7", StringComparison.Ordinal))
                approvedName = "手压水泵";
            else if (objectName.StartsWith("tripo_convert_c2c2230e-0627-436d-9a50-7dd939f0e4c3", StringComparison.Ordinal))
                approvedName = "街边金属长椅";
            else if (objectName.StartsWith("tripo_convert_6524e622-a9af-4f19-9d44-d03d23a4ea5b", StringComparison.Ordinal))
                approvedName = "金属货箱";
            else if (objectName.StartsWith("tripo_convert_9e43d7f3-ff46-4c4f-90b8-b13cc38b3b5f", StringComparison.Ordinal))
                approvedName = "齿轮装饰街道筒";
            return approvedName.Length > 0;
        }

        public void DebugSetPullStage(int stage) => PullStagesCompleted = Mathf.Clamp(stage, 0, Mathf.Max(1, pullStagesRequired));

        public event Action<GravityTarget> Destroyed;

        private GravityGameManager game;
        private Collider[] colliders;
        private Renderer[] renderers;
        private Vector3 heldCenterOffset;
        private Quaternion heldRotationOffset = Quaternion.identity;
        private Vector3 heldVisualBaseScale;
        private bool heldVisualScaleInitialized;
        private float struggleSeed;
        private bool dissolving;
        private static PhysicsMaterial restingPropMaterial;

        public static PhysicsMaterial RestingPropMaterial
        {
            get
            {
                if (restingPropMaterial == null)
                    restingPropMaterial = new PhysicsMaterial("Gravity props — non-bouncing contact")
                    {
                        bounciness = 0f,
                        dynamicFriction = .65f,
                        staticFriction = .75f,
                        bounceCombine = PhysicsMaterialCombine.Minimum,
                        frictionCombine = PhysicsMaterialCombine.Average
                    };
                return restingPropMaterial;
            }
        }

        private void Awake()
        {
            Body = GetComponent<Rigidbody>();
            RefreshGeometryCache();
            ConfigurePropContactGeometry();
            Health = Mathf.Max(1, maxHealth);
            BlockAvailable = canBlockOnce && kind != GravityTargetKind.Projectile;
            if (!IsScenePreview)
            {
                game = FindFirstObjectByType<GravityGameManager>();
                game?.Register(this);
            }
            struggleSeed = UnityEngine.Random.Range(0f, 100f);
            CacheHeldVisualScale();
        }

        private void Start()
        {
            if (IsScenePreview) return;
            game ??= FindFirstObjectByType<GravityGameManager>();
            game?.Register(this);
            CacheHeldVisualScale();
        }

        private void OnDestroy()
        {
            if (IsScenePreview) return;
            game?.Unregister(this);
        }

        public void RefreshGeometryCache()
        {
            colliders = GetComponentsInChildren<Collider>(true)
                .Where(c => c.GetComponentInParent<GravityTarget>() == this).ToArray();
            // Interaction beams, rings and particles are not part of the object's shape.
            renderers = GetComponentsInChildren<Renderer>(true)
                .Where(r => (r is MeshRenderer || r is SkinnedMeshRenderer)
                            && r.GetComponentInParent<GravityTarget>() == this).ToArray();
            CacheHeldVisualScale();
        }

        /// <summary>Fit authored prop colliders in their own coordinate system, including rotated imports.</summary>
        public void ConfigurePropContactGeometry()
        {
            if (kind != GravityTargetKind.Prop || !MatchesApprovedPropIdentity()) return;
            Body ??= GetComponent<Rigidbody>();
            if (colliders == null || renderers == null) RefreshGeometryCache();
            // These scene props each have one authored box. Keep future compound colliders intact.
            if (colliders.Length == 1 && colliders[0] is BoxCollider box
                && TryGetLocalGeometryBounds(box.transform, out var localBounds))
            {
                box.center = localBounds.center;
                box.size = Vector3.Max(localBounds.size, Vector3.one * .005f);
            }
            foreach (var item in colliders)
            {
                item.sharedMaterial = RestingPropMaterial;
                item.contactOffset = .005f;
            }
            Body.linearDamping = .06f;
            Body.angularDamping = .8f;
            Body.solverIterations = 12;
            Body.solverVelocityIterations = 4;
            Body.maxDepenetrationVelocity = 2f;
            Body.sleepThreshold = .008f;
        }

        public bool TryGetLocalGeometryBounds(Transform relativeTo, out Bounds bounds)
        {
            bounds = default;
            bool found = false;
            var geometry = renderers ?? GetComponentsInChildren<Renderer>(true);
            foreach (var renderer in geometry)
            {
                if (renderer == null || !(renderer is MeshRenderer || renderer is SkinnedMeshRenderer)
                    || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;
                var local = renderer.localBounds;
                var matrix = relativeTo.worldToLocalMatrix * renderer.localToWorldMatrix;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    var point = matrix.MultiplyPoint3x4(corner);
                    if (!found) { bounds = new Bounds(point, Vector3.zero); found = true; }
                    else bounds.Encapsulate(point);
                }
            }
            return found && bounds.extents.sqrMagnitude > .000001f;
        }

        public IReadOnlyList<Collider> ContactColliders => colliders ?? Array.Empty<Collider>();

        public Bounds ContactBounds
        {
            get
            {
                bool found = false;
                var bounds = new Bounds(transform.position, Vector3.zero);
                if (colliders == null) return bounds;
                foreach (var collider in colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    var itemBounds = collider.bounds;
                    if (collider is BoxCollider box)
                    {
                        // BoxCollider.bounds may still describe the previous physics step while held.
                        itemBounds = TransformBounds(new Bounds(box.center, box.size), box.transform.localToWorldMatrix);
                    }
                    if (!found) { bounds = itemBounds; found = true; }
                    else bounds.Encapsulate(itemBounds);
                }
                return bounds;
            }
        }

        public Bounds PhysicalContactBounds
        {
            get
            {
                if (Body == null || colliders == null || colliders.Length == 0) return ContactBounds;
                bool found = false;
                var bounds = new Bounds(Body.position, Vector3.zero);
                var physicalRoot = Matrix4x4.TRS(Body.position, Body.rotation, transform.lossyScale);
                foreach (var collider in colliders)
                {
                    if (collider == null || !collider.enabled || !collider.gameObject.activeInHierarchy) continue;
                    var itemBounds = collider.bounds;
                    if (collider is BoxCollider box)
                    {
                        var childFromRoot = transform.worldToLocalMatrix * box.transform.localToWorldMatrix;
                        itemBounds = TransformBounds(new Bounds(box.center, box.size), physicalRoot * childFromRoot);
                    }
                    if (!found) { bounds = itemBounds; found = true; }
                    else bounds.Encapsulate(itemBounds);
                }
                return bounds;
            }
        }

        private static Bounds TransformBounds(Bounds local, Matrix4x4 matrix)
        {
            var x = matrix.MultiplyVector(new Vector3(local.extents.x, 0, 0));
            var y = matrix.MultiplyVector(new Vector3(0, local.extents.y, 0));
            var z = matrix.MultiplyVector(new Vector3(0, 0, local.extents.z));
            var extents = new Vector3(Mathf.Abs(x.x) + Mathf.Abs(y.x) + Mathf.Abs(z.x),
                Mathf.Abs(x.y) + Mathf.Abs(y.y) + Mathf.Abs(z.y), Mathf.Abs(x.z) + Mathf.Abs(y.z) + Mathf.Abs(z.z));
            return new Bounds(matrix.MultiplyPoint3x4(local.center), extents * 2f);
        }

        public void ActivatePhysicsForInteraction(bool gravity = true)
        {
            if (Body == null || !IsAlive) return;
            // Keep both physics pose axes in sync before changing trigger/solid contact state.
            Body.position = transform.position;
            Body.rotation = transform.rotation;
            // Resolve newly-solid props before the solver sees them inside the street or steps.
            if (gravity && !IsHeld)
            {
                SetCollisionMode(true);
                GravityArchitectureCollision.ResolvePenetration(this);
                GravityGroundSurface.Active?.ResolvePenetration(this);
            }
            Body.isKinematic = false;
            Body.detectCollisions = true;
            Body.useGravity = gravity && kind != GravityTargetKind.Projectile && kind != GravityTargetKind.Heal;
            // Speculative CCD includes angular motion: sweep CCD misses a tall prop tipping
            // into the street after its first contact or an off-centre release.
            Body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Body.WakeUp();
        }

        private void FixedUpdate()
        {
            if (IsAlive && !IsHeld && kind == GravityTargetKind.Heal && Body != null && !Body.isKinematic)
            {
                // Heal pickups remain triggers for collection, but their free flight still stops at buildings.
                GravityArchitectureCollision.ResolvePenetration(this);
                var movement = Body.linearVelocity * Time.fixedDeltaTime;
                if (GravityArchitectureCollision.CastTarget(this, movement, out var hit))
                {
                    var travel = movement.normalized * Mathf.Min(movement.magnitude,
                        Mathf.Max(0, hit.distance - GravityArchitectureCollision.Skin));
                    transform.position += travel;
                    Body.position = transform.position;
                    float speed = Vector3.Dot(Body.linearVelocity, hit.normal);
                    if (speed < 0) Body.linearVelocity -= hit.normal * speed;
                }
            }
            // Ordinary resting contacts belong to PhysX. Never repeatedly lift a settling body.
            if (IsAlive && !IsHeld && Body != null && !Body.isKinematic && Body.useGravity && !Body.IsSleeping())
                GravityGroundSurface.Active?.RecoverFallenTarget(this);
        }

        public bool RequestGrab(GravityHand hand)
        {
            if (!IsAlive || HeldBy != null || hand == null) return false;
            if (PullStagesCompleted + 1 < Mathf.Max(1, pullStagesRequired))
            {
                PullStagesCompleted++;
                ActivatePhysicsForInteraction();
                var direction = (hand.holdAnchor.position - Center).normalized;
                Body.AddForce(direction * 5.5f + Vector3.up * 1.2f, ForceMode.VelocityChange);
                GravityFeedback.TargetPulse(Center, new Color(.42f, .3f, 1f), 1.15f);
                GravityAudio.Play(GravitySound.Suction, Center, .82f);
                hand.PulsePattern(.28f, .065f, 2);
                game?.SetStatus($"{displayName} 质量过大：引力锁定 {PullStagesCompleted}/{pullStagesRequired}");
                return false;
            }

            PullStagesCompleted = Mathf.Max(PullStagesCompleted, pullStagesRequired);
            Grab(hand);
            return true;
        }

        public void Grab(GravityHand hand)
        {
            if (!IsAlive || HeldBy != null || hand == null) return;
            HeldBy = hand;
            heldCenterOffset = transform.InverseTransformPoint(Center);
            heldRotationOffset = Quaternion.Inverse(hand.holdAnchor.rotation) * transform.rotation;
            ActivatePhysicsForInteraction(false);
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            SetCollisionMode(false);
            GravityFeedback.AttachGravityField(this, hand);
            GravityAudio.Play(GravitySound.Suction, Center, kind == GravityTargetKind.Robot ? 1f : .86f);
        }

        public void FollowPose(Vector3 desiredCenter, Quaternion handRotation, float deltaTime)
        {
            if (!IsHeld || !IsAlive) return;
            var offset = Vector3.zero;
            var rotation = handRotation * heldRotationOffset;
            if (kind == GravityTargetKind.Robot)
            {
                var time = Time.time + struggleSeed;
                offset = new Vector3(Mathf.Sin(time * 2.15f), Mathf.Sin(time * 1.65f) * .42f, Mathf.Cos(time * 1.9f)) * Mathf.Max(heldStruggle, .048f);
                var viewer = game?.playerHead ?? Camera.main?.transform;
                if (viewer != null)
                    rotation = RobotEnemy.RootRotationFacing(viewer.position - transform.position);
            }

            var mass = Body != null ? Mathf.Max(.35f, Body.mass) : 1f;
            var massResponse = 1f / (1f + Mathf.Max(0f, mass - 1f) * .18f);
            var stageResponse = 1f / (1f + Mathf.Max(0, pullStagesRequired - 1) * .22f);
            var extent = WorldBounds.extents.magnitude;
            var sizeResponse = 1f / (1f + Mathf.Max(0f, extent - .65f) * .22f);
            var sharpness = heldFollowSharpness * massResponse * stageResponse * sizeResponse;
            sharpness = Mathf.Clamp(sharpness, kind == GravityTargetKind.Projectile ? 10f : 3.6f, 12f);
            var t = 1f - Mathf.Exp(-sharpness * deltaTime);
            var nextRotation = Quaternion.Slerp(transform.rotation, rotation, t);
            var rotatedCenterOffset = nextRotation * Vector3.Scale(heldCenterOffset, transform.lossyScale);
            var desiredRoot = desiredCenter + offset - rotatedCenterOffset;
            UpdateHeldVisualScale(deltaTime);
            GravityArchitectureCollision.MoveSafely(this, Vector3.Lerp(transform.position, desiredRoot, t), nextRotation);
            GravityGroundSurface.Active?.ResolvePenetration(this);
            GravityArchitectureCollision.ResolvePenetration(this);
            Body.position = transform.position;
            Body.rotation = transform.rotation;
            Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
        }

        public void Release(Vector3 velocity, Vector3 angularVelocity)
        {
            if (!IsAlive) return;
            HeldBy = null;
            RestoreHeldVisualScale();
            ActivatePhysicsForInteraction(kind != GravityTargetKind.Projectile);
            SetCollisionMode(true);
            Body.linearVelocity = velocity;
            Body.angularVelocity = angularVelocity;
            GravityArchitectureCollision.ResolvePenetration(this);
            GravityGroundSurface.Active?.ResolvePenetration(this);
            ThrownUntil = Time.time + 1.75f;
            GravityAudio.Play(GravitySound.Release, Center, .55f);
        }

        public void MarkAsReflected(float duration = 2f)
        {
            ThrownUntil = Time.time + duration;
            RepulsedUntil = Time.time + duration;
        }

        public void MarkAsRepulsed(float duration = 1.6f)
        {
            RepulsedUntil = Time.time + duration;
            ThrownUntil = Mathf.Max(ThrownUntil, Time.time + duration);
        }

        public bool ConsumeBlock()
        {
            if (!BlockAvailable || !IsHeld || kind == GravityTargetKind.Projectile) return false;
            BlockAvailable = false;
            return true;
        }

        public void Damage(int amount, string reason = "击破", bool grantsCharge = true)
        {
            if (!IsAlive || amount <= 0) return;
            Health -= amount;
            GravityFeedback.HitFlash(Center, kind == GravityTargetKind.Robot ? new Color(.2f, .78f, 1f) : new Color(.72f, .35f, 1f));
            GravityAudio.Play(GravitySound.Hit, Center, .78f);
            if (Health <= 0) DestroyTarget(reason, grantsCharge);
        }

        public void DestroyTarget(string reason = "分解", bool grantsCharge = true)
        {
            if (!IsAlive) return;
            IsAlive = false;
            HeldBy?.ForceForget(this);
            HeldBy = null;
            game ??= FindFirstObjectByType<GravityGameManager>();
            game?.Unregister(this);
            game?.OnTargetDestroyed(this, reason, grantsCharge);
            Destroyed?.Invoke(this);
            DisableCollision();
            if (!Body.isKinematic) Body.linearVelocity = Vector3.zero;
            Body.angularVelocity = Vector3.zero;
            Body.detectCollisions = false;
            Body.isKinematic = true;

            if (kind == GravityTargetKind.Robot && TryGetComponent<RobotBreakApartEffect>(out var breakApart))
            {
                breakApart.Explode();
                return;
            }

            if (!dissolving) StartCoroutine(DissolveRoutine(kind == GravityTargetKind.Prop));
        }

        public Bounds WorldBounds
        {
            get
            {
                if (kind == GravityTargetKind.Prop && colliders != null && colliders.Length > 0)
                    return ContactBounds;
                var activeColliders = colliders?.Where(c => c != null && c.enabled).ToArray();
                var activeRenderers = renderers?.Where(r => r != null && r.enabled).ToArray();
                var found = false;
                var bounds = new Bounds(transform.position, Vector3.one * .15f);
                if (activeColliders != null)
                {
                    foreach (var item in activeColliders)
                    {
                        if (!found) { bounds = item.bounds; found = true; }
                        else bounds.Encapsulate(item.bounds);
                    }
                }
                if (activeRenderers != null)
                {
                    foreach (var item in activeRenderers)
                    {
                        if (!found) { bounds = item.bounds; found = true; }
                        else bounds.Encapsulate(item.bounds);
                    }
                }
                return bounds;
            }
        }

        private void SetCollisionMode(bool enabled)
        {
            if (colliders == null) return;
            foreach (var item in colliders)
            {
                if (item == null) continue;
                item.enabled = true;
                // safeTriggerColliders only prevent authored props from fighting nearby
                // architecture before interaction. Released props must collide with ground.
                item.isTrigger = !enabled || kind == GravityTargetKind.Projectile || kind == GravityTargetKind.Heal;
            }
        }

        public bool IsSafeTriggerCollider(Collider collider)
        {
            return collider != null && safeTriggerColliders != null && safeTriggerColliders.Contains(collider);
        }

        public void ConfigureSafeTriggerColliders(IEnumerable<Collider> collidersToKeepAsTriggers)
        {
            safeTriggerColliders = collidersToKeepAsTriggers?.Where(collider => collider != null).Distinct().ToArray()
                                   ?? Array.Empty<Collider>();
        }

        public IReadOnlyCollection<Collider> SafeTriggerColliders => safeTriggerColliders;

        private void CacheHeldVisualScale()
        {
            if (kind != GravityTargetKind.Robot || replaceableVisual == null || heldVisualScaleInitialized) return;
            heldVisualBaseScale = replaceableVisual.localScale;
            heldVisualScaleInitialized = true;
        }

        private void UpdateHeldVisualScale(float deltaTime)
        {
            if (kind != GravityTargetKind.Robot || replaceableVisual == null) return;
            CacheHeldVisualScale();
            if (!heldVisualScaleInitialized) return;
            var targetScale = heldVisualBaseScale * .84f;
            var blend = 1f - Mathf.Exp(-6.5f * Mathf.Max(0f, deltaTime));
            replaceableVisual.localScale = Vector3.Lerp(replaceableVisual.localScale, targetScale, blend);
        }

        private void RestoreHeldVisualScale()
        {
            if (!heldVisualScaleInitialized || replaceableVisual == null) return;
            replaceableVisual.localScale = heldVisualBaseScale;
        }

        private void DisableCollision()
        {
            if (colliders == null) return;
            foreach (var item in colliders)
            {
                if (item != null) item.enabled = false;
            }
        }

        private IEnumerator DissolveRoutine(bool blockedProp)
        {
            dissolving = true;
            var originScale = transform.localScale;
            var center = Center;
            if (blockedProp)
            {
                GravityFeedback.BlockBreak(center, WorldBounds.extents.magnitude);
                GravityAudio.Play(GravitySound.ShieldBreak, center, 1f);
            }
            else GravityFeedback.TargetPulse(center, new Color(.3f, .88f, 1f), 1f);

            var duration = blockedProp ? .48f : .28f;
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var p = Mathf.Clamp01(time / duration);
                var kick = 1f + Mathf.Sin(p * Mathf.PI) * .16f;
                transform.localScale = originScale * Mathf.Lerp(kick, 0f, p * p);
                foreach (var renderer in renderers)
                {
                    if (renderer == null) continue;
                    var block = new MaterialPropertyBlock();
                    renderer.GetPropertyBlock(block);
                    block.SetColor("_EmissionColor", Color.Lerp(new Color(.82f, .44f, 1f) * 5f, Color.black, p));
                    renderer.SetPropertyBlock(block);
                }
                yield return null;
            }
            Destroy(gameObject);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (!IsAlive) return;
            if (RepulsedUntil > Time.time && collision.relativeVelocity.sqrMagnitude > 12f)
            {
                var point = collision.contactCount > 0 ? collision.GetContact(0).point : Center;
                GravityFeedback.EnvironmentImpact(point, collision.relativeVelocity.magnitude);
                GravityAudio.Play(GravitySound.EnvironmentImpact, point, Mathf.Clamp01(collision.relativeVelocity.magnitude / 12f));
                RepulsedUntil = 0f;
            }
            if (Time.time > ThrownUntil || Body.linearVelocity.sqrMagnitude < 10f) return;
            var other = collision.collider.GetComponentInParent<GravityTarget>();
            if (other == null || !other.IsAlive || other == this || other.kind != GravityTargetKind.Robot) return;
            other.Damage(kind == GravityTargetKind.Robot ? 2 : 1, displayName + "撞击");
            DestroyTarget(string.Empty, false);
        }
    }
}
