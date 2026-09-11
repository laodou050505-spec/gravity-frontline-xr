using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ByteDance.PICO.XR;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.XR;

namespace GravityRepulsion
{
    public readonly struct GravityRunSummary
    {
        public readonly int completedWaves;
        public readonly int enemiesDefeated;
        public readonly int baseCombatScore;
        public readonly int waveScore;
        public readonly int precisionDefenseCount;
        public readonly int precisionDefenseBonus;
        public readonly int reflectAttackCount;
        public readonly int reflectAttackBonus;
        public readonly int repulseAttackCount;
        public readonly int repulseAttackBonus;
        public readonly int collisionAttackCount;
        public readonly int collisionAttackBonus;
        public readonly int environmentAttackCount;
        public readonly int environmentAttackBonus;
        public readonly int ultimateAttackCount;
        public readonly int ultimateAttackBonus;

        public int TotalScore => baseCombatScore + waveScore + precisionDefenseBonus + reflectAttackBonus +
                                 repulseAttackBonus + collisionAttackBonus + environmentAttackBonus + ultimateAttackBonus;

        public GravityRunSummary(
            int completedWaves, int enemiesDefeated, int baseCombatScore, int waveScore,
            int precisionDefenseCount, int precisionDefenseBonus,
            int reflectAttackCount, int reflectAttackBonus,
            int repulseAttackCount, int repulseAttackBonus,
            int collisionAttackCount, int collisionAttackBonus,
            int environmentAttackCount, int environmentAttackBonus,
            int ultimateAttackCount, int ultimateAttackBonus)
        {
            this.completedWaves = completedWaves;
            this.enemiesDefeated = enemiesDefeated;
            this.baseCombatScore = baseCombatScore;
            this.waveScore = waveScore;
            this.precisionDefenseCount = precisionDefenseCount;
            this.precisionDefenseBonus = precisionDefenseBonus;
            this.reflectAttackCount = reflectAttackCount;
            this.reflectAttackBonus = reflectAttackBonus;
            this.repulseAttackCount = repulseAttackCount;
            this.repulseAttackBonus = repulseAttackBonus;
            this.collisionAttackCount = collisionAttackCount;
            this.collisionAttackBonus = collisionAttackBonus;
            this.environmentAttackCount = environmentAttackCount;
            this.environmentAttackBonus = environmentAttackBonus;
            this.ultimateAttackCount = ultimateAttackCount;
            this.ultimateAttackBonus = ultimateAttackBonus;
        }
    }

    public sealed class GravityGameManager : MonoBehaviour
    {
        [Header("Player")]
        public Transform playerHead;
        public GravityDesignStart designStart;
        public GravityHand leftHand;
        public GravityHand rightHand;
        public GravityHud hud;

        [Header("Prefabs")]
        public GameObject scoutRobotPrefab;
        public GameObject engineerRobotPrefab;
        public GameObject guardRobotPrefab;
        public GameObject healthPackPrefab;

        [Header("Arena")]
        public Transform[] spawnPoints;
        public Transform supplyPoint;
        public float repulseCooldownDuration = 3.8f;
        public float intermissionDuration = 3.4f;

        public int Wave { get; private set; } = 1;
        public int CompletedWaves { get; private set; }
        public int Score => BuildRunSummary().TotalScore;
        public int EnemiesDefeated { get; private set; }
        public float Shield { get; private set; } = 100f;
        public float UltimateCharge { get; private set; }
        public int UltimateRequirement { get; private set; } = 100;
        public float RepulseCooldown { get; private set; }
        public bool UltimateActive => ultimateActive;
        public IReadOnlyList<GravityTarget> AliveEnemies => targets.Where(t => t != null && t.IsAlive && t.gameObject.activeInHierarchy && t.kind == GravityTargetKind.Robot).ToList();
        public int RegisteredScenePreviewCount => targets.Count(t => t != null && t.IsScenePreview);
        public bool IsRegistered(GravityTarget target) => target != null && targets.Contains(target);
        public int ProjectileConcurrencyLimit => Mathf.Min(5, 2 + (Wave - 1) / 4);
        public float EnemyShotInterval => Mathf.Max(.9f, 1.42f - Wave * .025f);
        public bool CanEnemyFire => !gameOver && !ultimateActive && globalShotCooldown <= 0f && ActiveProjectileCount < ProjectileConcurrencyLimit;

        private readonly List<GravityTarget> targets = new();
        private bool combatStarted;
        private bool intermission;
        private bool gameOver;
        private bool ultimateActive;
        private float zeroEnemyConfirmTimer;
        private float intermissionTimer;
        private float globalShotCooldown;
        private float damageCooldown;
        private float damageGrace;
        private bool leftPrimaryWasPressed;
        private bool rightPrimaryWasPressed;
        private bool leftSecondaryWasPressed;
        private float lastCollisionTime = -999f;
        private int baseCombatScore;
        private int waveScore;
        private int precisionDefenseCount;
        private int precisionDefenseBonus;
        private int reflectAttackCount;
        private int reflectAttackBonus;
        private int repulseAttackCount;
        private int repulseAttackBonus;
        private int collisionAttackCount;
        private int collisionAttackBonus;
        private int environmentAttackCount;
        private int environmentAttackBonus;
        private int ultimateAttackCount;
        private int ultimateAttackBonus;

        private int ActiveProjectileCount => targets.Count(t => t != null && t.IsAlive && t.gameObject.activeInHierarchy && t.kind == GravityTargetKind.Projectile);

        private void Start()
        {
            playerHead ??= Camera.main?.transform;
            GravityMusic.EnsurePlaying();
            var preview = GameObject.Find("SCENE_PREVIEW_ENEMIES_仅Scene查看");
            if (preview != null)
            {
                foreach (var previewTarget in preview.GetComponentsInChildren<GravityTarget>(true)) targets.Remove(previewTarget);
                preview.SetActive(false);
            }
            StartCombat();
        }

        private void Update()
        {
            if (gameOver)
            {
                if (Keyboard.current?.enterKey.wasPressedThisFrame == true) RestartGame();
                ReadPrimary(XRNode.RightHand, ref rightPrimaryWasPressed, RestartGame);
                return;
            }
            RepulseCooldown = Mathf.Max(0f, RepulseCooldown - Time.deltaTime);
            globalShotCooldown = Mathf.Max(0f, globalShotCooldown - Time.deltaTime);
            damageCooldown = Mathf.Max(0f, damageCooldown - Time.deltaTime);
            damageGrace = Mathf.Max(0f, damageGrace - Time.deltaTime);
            ReadAbilities();
            CheckHeldCollision();
            CheckWaveState();
            hud?.Refresh(this);
        }

        public void Register(GravityTarget target)
        {
            if (target != null && !target.IsScenePreview && !targets.Contains(target)) targets.Add(target);
        }

        public void Unregister(GravityTarget target)
        {
            targets.Remove(target);
        }

        public void OnTargetDestroyed(GravityTarget target, string reason, bool grantsCharge)
        {
            if (target.kind == GravityTargetKind.Robot)
            {
                RecordEnemyScore(reason);
                if (grantsCharge && !ultimateActive)
                    UltimateCharge = Mathf.Min(UltimateRequirement, UltimateCharge + Mathf.Max(1, target.ultimateCharge));
                GravityFeedback.Burst(target.transform.position + Vector3.up, target.maxHealth > 1 ? new Color(1f, .25f, .35f) : new Color(.25f, .85f, 1f), 26, 1.2f);
            }
            else if (target.kind == GravityTargetKind.Projectile)
            {
                GravityFeedback.Burst(target.transform.position, new Color(1f, .18f, .32f), 12, .7f);
            }
            else
            {
                GravityFeedback.Burst(target.transform.position, new Color(.72f, .38f, 1f), 14, .75f);
            }
            if (!string.IsNullOrWhiteSpace(reason)) hud?.SetStatus(reason);
        }

        public void SetStatus(string message) => hud?.SetStatus(message);
        public void DebugReadyUltimate() => UltimateCharge = UltimateRequirement;

        public GravityRunSummary BuildRunSummary()
        {
            return new GravityRunSummary(
                CompletedWaves, EnemiesDefeated, baseCombatScore, waveScore,
                precisionDefenseCount, precisionDefenseBonus,
                reflectAttackCount, reflectAttackBonus,
                repulseAttackCount, repulseAttackBonus,
                collisionAttackCount, collisionAttackBonus,
                environmentAttackCount, environmentAttackBonus,
                ultimateAttackCount, ultimateAttackBonus);
        }

        public static GravityRunSummary BuildEvidenceRunSummary()
        {
            return new GravityRunSummary(
                3, 26, 650, 450,
                2, 120,
                2, 160,
                3, 135,
                2, 100,
                1, 60,
                4, 140);
        }

        public void NotifyEnemyFired()
        {
            globalShotCooldown = EnemyShotInterval;
        }

        public bool TryBlockProjectile(EnemyProjectile projectile)
        {
            if (projectile == null || projectile.Target == null) return false;
            foreach (var hand in new[] { leftHand, rightHand })
            {
                var guard = hand?.Held;
                if (guard == null || !guard.IsAlive || guard.kind == GravityTargetKind.Projectile) continue;
                if (Vector3.Distance(guard.WorldBounds.center, projectile.transform.position) > guard.WorldBounds.extents.magnitude + .75f) continue;
                if (!guard.ConsumeBlock()) continue;
                projectile.Target.DestroyTarget(string.Empty, false);
                GravityFeedback.BlockBreak(guard.Center, guard.WorldBounds.extents.magnitude);
                GravityAudio.Play(GravitySound.ShieldBreak, guard.Center, 1f);
                guard.DestroyTarget("手持物挡下一次攻击：防御物裂解", false);
                hand.Pulse(.65f, .12f);
                precisionDefenseCount++;
                precisionDefenseBonus += 60;
                hud?.SetStatus("精准防御  +60");
                return true;
            }
            return false;
        }

        public void DamagePlayer(int amount)
        {
            if (gameOver || ultimateActive || damageGrace > 0f || damageCooldown > 0f) return;
            damageCooldown = 1.4f;
            Shield = Mathf.Max(0f, Shield - amount);
            hud?.Refresh(this);
            hud?.SetStatus($"护盾受损  -{amount}");
            GravityFeedback.Shockwave(playerHead != null ? playerHead.position : Vector3.zero, new Color(1f, .08f, .2f), 1.4f);
            GravityAudio.Play(GravitySound.PlayerHurt, playerHead != null ? playerHead.position : Vector3.zero, .92f);
            if (Shield > 0f) return;
            gameOver = true;
            leftHand?.Held?.Release(Vector3.zero, Vector3.zero);
            rightHand?.Held?.Release(Vector3.zero, Vector3.zero);
            FreezeRunAtEnd();
            hud?.Refresh(this);
            hud?.ShowRunSummary(BuildRunSummary());
            GravityAudio.Play(GravitySound.UiReady, playerHead != null ? playerHead.position : Vector3.zero, .86f, .92f);
        }

        public void ConsumeHealthPack(GravityTarget pack)
        {
            if (pack == null || pack.kind != GravityTargetKind.Heal) return;
            Shield = Mathf.Min(100f, Shield + 35f);
            hud?.Refresh(this);
            GravityAudio.Play(GravitySound.Pickup, pack.Center, 1f);
            GravityFeedback.TargetPulse(pack.Center, new Color(.2f, 1f, .62f), 2f);
            GravityFeedback.Burst(pack.Center, new Color(.18f, 1f, .52f), 36, 1.45f);
            pack.DestroyTarget("护盾恢复 +35", false);
        }

        public GravityTarget NearestAvailableProp(Vector3 point)
        {
            return targets.Where(t => t != null && t.IsAlive && t.gameObject.activeInHierarchy && !t.IsHeld && t.kind == GravityTargetKind.Prop && t.HasValidVisibleInteractionGeometry(out _))
                .OrderBy(t => Vector3.Distance(point, t.transform.position)).FirstOrDefault();
        }

        public void Repulse()
        {
            if (gameOver || ultimateActive) return;
            if (RepulseCooldown > 0f)
            {
                hud?.SetStatus($"斥力冷却 {RepulseCooldown:0.0}s");
                return;
            }
            RepulseCooldown = repulseCooldownDuration;
            var origin = playerHead != null ? playerHead.position + playerHead.forward * 1.5f : Vector3.up;
            var hitCount = 0;
            foreach (var target in targets.ToArray())
            {
                if (target == null || !target.IsAlive || !target.gameObject.activeInHierarchy || target.IsHeld) continue;
                var delta = target.transform.position - origin;
                var distance = delta.magnitude;
                if (distance > 11f || Vector3.Dot(playerHead.forward, delta.normalized) < -.1f) continue;
                target.ActivatePhysicsForInteraction(target.kind != GravityTargetKind.Projectile);
                target.Body.AddForce(delta.normalized * Mathf.Lerp(20f, 4f, distance / 11f) + Vector3.up * 3f, ForceMode.VelocityChange);
                target.MarkAsRepulsed();
                if (target.kind == GravityTargetKind.Projectile)
                {
                    target.GetComponent<EnemyProjectile>()?.Reflect(delta.normalized);
                }
                else if (target.kind == GravityTargetKind.Robot && (distance < 5.3f || target.maxHealth == 1))
                {
                    target.Damage(1, "斥力崩解");
                }
                hitCount++;
            }
            leftHand?.Pulse(.48f, .11f);
            rightHand?.Pulse(.48f, .11f);
            GravityFeedback.Shockwave(origin, new Color(.68f, .35f, 1f), .55f);
            GravityAudio.Play(GravitySound.Repulse, origin, 1f);
            hud?.SetStatus(hitCount > 0 ? $"斥力爆发  击退 {hitCount} 个目标" : "斥力爆发  空域净空");
        }

        public void CollideHeld()
        {
            if (Time.time - lastCollisionTime < .4f) return;
            var a = leftHand?.Held;
            var b = rightHand?.Held;
            if (a == null || b == null || !a.IsAlive || !b.IsAlive)
            {
                hud?.SetStatus("需要双手各锁定一个目标");
                return;
            }
            lastCollisionTime = Time.time;
            var midpoint = (a.WorldBounds.center + b.WorldBounds.center) * .5f;
            foreach (var robot in AliveEnemies.ToArray())
            {
                if (robot == a || robot == b) continue;
                if (Vector3.Distance(robot.transform.position, midpoint) < 3.2f) robot.Damage(1, "对撞冲击");
            }
            GravityFeedback.Burst(midpoint, new Color(.82f, .52f, 1f), 52, 1.7f);
            GravityFeedback.Shockwave(midpoint, new Color(.72f, .28f, 1f), 4.2f);
            leftHand.Pulse(.8f, .16f);
            rightHand.Pulse(.8f, .16f);
            a.DestroyTarget("双极对撞", true);
            b.DestroyTarget("双极对撞", true);
            hud?.SetStatus("双极对撞  完美分解");
        }

        public void ActivateUltimate()
        {
            if (gameOver || ultimateActive) return;
            if (UltimateCharge < UltimateRequirement)
            {
                hud?.SetStatus($"奇点能量不足 {Mathf.FloorToInt(UltimateCharge)}/{UltimateRequirement}");
                return;
            }
            if (AliveEnemies.Count == 0)
            {
                hud?.SetStatus("当前没有可收束的敌人");
                return;
            }
            UltimateCharge = 0f;
            UltimateRequirement = Mathf.Min(600, UltimateRequirement + 120);
            StartCoroutine(UltimateRoutine());
        }

        public void Recenter()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            PXR_Plugin.Sensor.UPxr_ResetSensor(ResetSensorOption.ResetAll);
#endif
            // Reset the physical tracking reference first, then put the XR Origin back at the
            // same authored battle-axis start used by desktop. Local headset movement remains
            // available after this, but the world root never drifts below/behind the encounter.
            designStart?.ApplyToRig();
            hud?.SetStatus("战场起点与追踪原点已重新校准");
        }

        private IEnumerator UltimateRoutine()
        {
            ultimateActive = true;
            var center = playerHead.position + playerHead.forward * 13f + Vector3.up * 6f;
            var core = GravityFeedback.CreateSingularity(center);
            var victims = AliveEnemies.ToArray();
            var duration = 4.25f;
            var announcedPhase = 0;
            GravityAudio.Play(GravitySound.UltimateCharge, center, 1f);
            hud?.SetStatus("地爆天星：引力锚定");
            for (var time = 0f; time < duration; time += Time.deltaTime)
            {
                var progress = time / duration;
                var phaseIndex = progress < .24f ? 0 : progress < .58f ? 1 : progress < .84f ? 2 : 3;
                if (phaseIndex != announcedPhase)
                {
                    announcedPhase = phaseIndex;
                    if (phaseIndex == 1)
                    {
                        hud?.SetStatus("地爆天星：群体环绕");
                        GravityFeedback.Shockwave(center, new Color(.28f, .68f, 1f), 6f);
                    }
                    else if (phaseIndex == 2)
                    {
                        hud?.SetStatus("地爆天星：奇点压缩");
                        GravityAudio.Play(GravitySound.UltimateCompress, center, 1f);
                    }
                    else if (phaseIndex == 3)
                    {
                        hud?.SetStatus("地爆天星：湮灭爆发");
                        GravityFeedback.TargetPulse(center, new Color(.92f, .72f, 1f), 3.2f);
                    }
                }
                var coreScale = phaseIndex switch
                {
                    0 => Mathf.Lerp(.45f, 1.25f, progress / .24f),
                    1 => 1.25f + Mathf.Sin(time * 9f) * .1f,
                    2 => Mathf.Lerp(1.25f, .38f, (progress - .58f) / .26f),
                    _ => Mathf.Lerp(.38f, 1.65f, (progress - .84f) / .16f)
                };
                core.transform.localScale = Vector3.one * coreScale;
                for (var i = 0; i < victims.Length; i++)
                {
                    var enemy = victims[i];
                    if (enemy == null || !enemy.IsAlive) continue;
                    var phase = i * 1.73f;
                    var stageProgress = phaseIndex == 0 ? progress / .24f : phaseIndex == 1 ? (progress - .24f) / .34f : phaseIndex == 2 ? (progress - .58f) / .26f : 1f;
                    var radius = phaseIndex == 0 ? Mathf.Lerp(5.2f, 3.6f, stageProgress) : phaseIndex == 1 ? 3.6f : phaseIndex == 2 ? Mathf.Lerp(3.6f, .42f, stageProgress * stageProgress) : .35f;
                    var orbit = new Vector3(Mathf.Sin(time * (phaseIndex == 1 ? 5.5f : 3.5f) + phase) * radius, Mathf.Cos(time * 4f + phase) * radius * .45f, Mathf.Cos(time * 5.1f + phase) * radius);
                    enemy.Body.useGravity = false;
                    enemy.Body.linearVelocity = Vector3.zero;
                    var nextPosition = Vector3.Lerp(enemy.transform.position, center + orbit,
                        Time.deltaTime * (phaseIndex == 0 ? 2.8f : phaseIndex == 2 ? 10f : 6f));
                    var nextRotation = enemy.transform.rotation * Quaternion.Euler(220f * Time.deltaTime, 300f * Time.deltaTime, 0f);
                    GravityArchitectureCollision.MoveSafely(enemy, nextPosition, nextRotation);
                }
                yield return null;
            }
            foreach (var enemy in victims) if (enemy != null && enemy.IsAlive) enemy.DestroyTarget("奇点湮灭", false);
            GravityFeedback.Burst(center, new Color(.92f, .72f, 1f), 120, 2.5f);
            GravityFeedback.Shockwave(center, new Color(.75f, .25f, 1f), 10f);
            GravityAudio.Play(GravitySound.UltimateBlast, center, 1f);
            Destroy(core);
            hud?.SetStatus("奇点坍缩  大道净空");
            yield return new WaitForSeconds(.55f);
            ultimateActive = false;
        }

        private void StartCombat()
        {
            combatStarted = true;
            SpawnWave(8);
        }

        private void SpawnWave(int count)
        {
            intermission = false;
            damageGrace = 1.35f;
            for (var i = 0; i < count; i++)
            {
                var prefab = (i + Wave) % 6 == 0 ? guardRobotPrefab : i % 5 == 0 ? engineerRobotPrefab : scoutRobotPrefab;
                if (prefab == null || spawnPoints == null || spawnPoints.Length == 0) continue;
                var point = spawnPoints[i % spawnPoints.Length];
                var row = i / spawnPoints.Length;
                var laneOffset = point.right * ((row % 2 == 0 ? -1f : 1f) * row * 1.15f);
                var spawnPosition = point.position + point.forward * row * 4.8f + laneOffset;
                spawnPosition.y = Mathf.Max(spawnPosition.y + .12f, .12f);
                var spawnRotation = playerHead != null ? RobotEnemy.RootRotationFacing(playerHead.position - spawnPosition) : point.rotation * Quaternion.Euler(0f, 180f, 0f);
                var spawned = Instantiate(prefab, spawnPosition, spawnRotation);
                if (spawned.TryGetComponent<GravityTarget>(out var target))
                {
                    GravityArchitectureCollision.ResolvePenetration(target);
                    GravityGroundSurface.Active?.ResolvePenetration(target);
                }
                if (spawned.TryGetComponent<Rigidbody>(out var body)) body.linearVelocity = Vector3.zero;
            }
            hud?.SetStatus(Wave == 1
                ? "对准敌人 · 扳机抓取 · 松开投掷"
                : $"第 {Wave:00} 波侵入者抵达 · 锁定目标");
        }

        private void CheckWaveState()
        {
            if (!combatStarted || ultimateActive || gameOver) return;
            if (!intermission && AliveEnemies.Count == 0)
            {
                zeroEnemyConfirmTimer += Time.deltaTime;
                if (zeroEnemyConfirmTimer < .2f) return;
                intermission = true;
                CompletedWaves = Wave;
                waveScore += 100 + Wave * 25;
                var supplied = false;
                if (Wave % 3 == 0 && Shield < 92f && healthPackPrefab != null && supplyPoint != null && !targets.Any(t => t != null && t.IsAlive && t.kind == GravityTargetKind.Heal))
                {
                    var supply = Instantiate(healthPackPrefab, supplyPoint.position, supplyPoint.rotation);
                    if (supply.TryGetComponent<GravityTarget>(out var pack))
                        GravityArchitectureCollision.ResolvePenetration(pack);
                    supplied = true;
                    hud?.SetStatus("防卫补给已投放  吸取即可恢复");
                }
                else hud?.SetStatus($"第 {Wave} 波守护完成  总分 {Score}");
                intermissionTimer = supplied ? 5.5f : intermissionDuration;
            }
            else if (!intermission) zeroEnemyConfirmTimer = 0f;
            if (!intermission) return;
            intermissionTimer -= Time.deltaTime;
            if (intermissionTimer > 0f) return;
            foreach (var projectile in targets.Where(t => t != null && t.IsAlive && t.kind == GravityTargetKind.Projectile).ToArray()) projectile.DestroyTarget(string.Empty, false);
            Wave++;
            SpawnWave(Mathf.Min(18, 6 + Wave + Wave / 3));
            zeroEnemyConfirmTimer = 0f;
        }

        private void RecordEnemyScore(string reason)
        {
            EnemiesDefeated++;
            baseCombatScore += 25;
            if (string.IsNullOrWhiteSpace(reason)) return;
            if (reason.Contains("反射"))
            {
                reflectAttackCount++;
                reflectAttackBonus += 80;
            }
            else if (reason.Contains("斥力"))
            {
                repulseAttackCount++;
                repulseAttackBonus += 45;
            }
            else if (reason.Contains("双极") || reason.Contains("对撞"))
            {
                collisionAttackCount++;
                collisionAttackBonus += 50;
            }
            else if (reason.Contains("环境连锁"))
            {
                environmentAttackCount++;
                environmentAttackBonus += 60;
            }
            else if (reason.Contains("奇点"))
            {
                ultimateAttackCount++;
                ultimateAttackBonus += 35;
            }
        }

        private void FreezeRunAtEnd()
        {
            foreach (var projectile in targets.Where(t => t != null && t.IsAlive && t.kind == GravityTargetKind.Projectile).ToArray())
                projectile.DestroyTarget(string.Empty, false);
            foreach (var robot in targets.Where(t => t != null && t.IsAlive && t.kind == GravityTargetKind.Robot).ToArray())
            {
                if (robot.TryGetComponent<RobotEnemy>(out var enemy)) enemy.enabled = false;
                robot.GetComponent<RobotRigMotion>()?.DebugSetPose(0f, false, false);
                if (robot.Body == null) continue;
                robot.Body.linearVelocity = Vector3.zero;
                robot.Body.angularVelocity = Vector3.zero;
                robot.Body.isKinematic = true;
            }
        }

        private void CheckHeldCollision()
        {
            if (!XRSettings.isDeviceActive || leftHand?.Held == null || rightHand?.Held == null) return;
            if (leftHand.Held.WorldBounds.Intersects(rightHand.Held.WorldBounds)) CollideHeld();
        }

        private void ReadAbilities()
        {
            var keyboard = Keyboard.current;
            if (keyboard?.fKey.wasPressedThisFrame == true) Repulse();
            if (keyboard?.rKey.wasPressedThisFrame == true) ActivateUltimate();
            if (keyboard?.spaceKey.wasPressedThisFrame == true) CollideHeld();

            ReadPrimary(XRNode.LeftHand, ref leftPrimaryWasPressed, Repulse);
            ReadPrimary(XRNode.RightHand, ref rightPrimaryWasPressed, ActivateUltimate);
            ReadSecondary(XRNode.LeftHand, ref leftSecondaryWasPressed, Recenter);
        }

        private static void ReadPrimary(XRNode node, ref bool wasPressed, System.Action action)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var pressed = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out var value) && value;
            if (pressed && !wasPressed) action?.Invoke();
            wasPressed = pressed;
        }

        private static void ReadSecondary(XRNode node, ref bool wasPressed, System.Action action)
        {
            var device = InputDevices.GetDeviceAtXRNode(node);
            var pressed = device.isValid && device.TryGetFeatureValue(UnityEngine.XR.CommonUsages.secondaryButton, out var value) && value;
            if (pressed && !wasPressed) action?.Invoke();
            wasPressed = pressed;
        }

        private static void RestartGame()
        {
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
