using System;
using System.Collections;
using System.Linq;
using UnityEngine;

namespace GravityRepulsion
{
    public sealed class GravityAutomatedSmoke : MonoBehaviour
    {
        public GravityGameManager game;

        private void Start()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravitySmoke")) return;
            StartCoroutine(Run());
        }

        private IEnumerator Run()
        {
            yield return new WaitForSeconds(.15f);
            if (!Check(game != null, "manager missing")) yield break;
            var stability = game.GetComponent<GravitySceneStabilityGuard>();
            if (!Check(stability != null, "scene stability guard missing")) yield break;
            while (!stability.Completed) yield return null;
            if (!Check(stability.Passed, "5-second scene stability failed: " + stability.FailureSummary)) yield break;
            if (!Check(game.RegisteredScenePreviewCount == 0, "scene-preview targets registered in gameplay")) yield break;
            if (!Check(game.leftHand != null && game.rightHand != null, "hands missing")) yield break;
            if (!Check(game.designStart != null && game.designStart.MatchesRig(), "desktop/XR rig does not use the single design player start")) yield break;
            var hud = FindFirstObjectByType<GravityHud>();
            if (!Check(hud != null && hud.EdgeLayoutConfigured && hud.CameraHudConfigured, "front-facing camera HUD missing")) yield break;
            if (!Check(hud.ValidateViewportLayout(Camera.main, out var hudFailure), "camera HUD placement failed: " + hudFailure)) yield break;
            if (!Check(hud.ValidateHandAvoidance(Camera.main, game.leftHand, game.rightHand, out var handHudFailure), "camera HUD binding failed: " + handHudFailure)) yield break;
            hud.ShowRunSummary(GravityGameManager.BuildEvidenceRunSummary());
            if (!Check(hud.ValidateResultLayout(Camera.main, out var resultFailure), "result side-card layout failed: " + resultFailure)) yield break;
            if (!Check(hud.ResultTextContent.Contains("TOTAL SCORE") && hud.ResultTextContent.Contains("WAVES CLEARED"), "result panel missing expected English content")) yield break;
            Debug.Log($"GRAVITY_CAMERA_HUD_PASS designStart={game.designStart.worldPosition}/{game.designStart.worldYaw:0.0}/{game.designStart.eyeHeight:0.000} profile={hud.ActiveSizeProfile} reference={hud.ActiveReferenceResolution.x:0}x{hud.ActiveReferenceResolution.y:0} controllerRays=worldspace");
            Debug.Log("GRAVITY_RESULT_PANEL_PASS cameraHud=true totalScore=true completedWaves=true rewardBreakdown=true failureCopy=false");
            hud.HideRunSummary();
            Debug.Log("GRAVITY_CAMERA_HUD_PASS canvas=ScreenSpaceCamera frontFacing=true gameplay=street_enemies_props_projectiles");
            if (!Check((!game.leftHand.gravityRay.enabled || game.leftHand.Candidate != null)
                       && (!game.rightHand.gravityRay.enabled || game.rightHand.Candidate != null), "controller ray was visible without a valid aim candidate")) yield break;
            if (!Check(game.AliveEnemies.Count == 8, "wave 1 did not spawn 8 enemies")) yield break;
            if (!Check(game.ProjectileConcurrencyLimit == 2 && game.EnemyShotInterval >= 1.35f && game.EnemyShotInterval <= 1.45f, "enemy projectile pacing was not moderately increased")) yield break;
            if (!Check(game.AliveEnemies.All(e => e.GetComponentsInChildren<Renderer>(true).Count(r => r is MeshRenderer || r is SkinnedMeshRenderer) >= 8), "29/30/31 real split robot meshes missing")) yield break;
            var projectilePrefabs = game.AliveEnemies.Select(e => e.GetComponent<RobotEnemy>()).Where(e => e != null).ToArray();
            if (!Check(game.AliveEnemies.All(e => e.heldFollowSharpness <= 10.1f), "enemy pull follow was not slowed")) yield break;
            var props = FindObjectsByType<GravityTarget>(FindObjectsInactive.Exclude, FindObjectsSortMode.None).Where(t => t.kind == GravityTargetKind.Prop).ToArray();
            if (!Check(props.Length > 0, "no approved prop whitelist entries")) yield break;
            foreach (var prop in props)
                if (!Check(prop.HasValidVisibleInteractionGeometry(out var propFailure), $"invalid prop {prop.name}/{prop.displayName}: {propFailure}")) yield break;
            if (!Check(props.All(prop => prop.MatchesApprovedPropIdentity()), "pullable whitelist contains a non-approved or misnamed scene model")) yield break;
            if (!Check(props.Select(prop => prop.displayName).Distinct().Count() >= 3, "fewer than three confirmed scene-model identities are pullable")) yield break;
            foreach (var requiredIdentity in GravityTarget.ApprovedScenePropDisplayNames)
                if (!Check(props.Any(prop => prop.displayName == requiredIdentity), $"required scene model is not pullable: {requiredIdentity}")) yield break;
            if (!Check(props.All(prop => prop.GetComponentsInChildren<Transform>(true).All(item => !item.name.StartsWith("VISUAL_可吸道具_", StringComparison.Ordinal))), "generated pullable visual found")) yield break;
            Debug.Log($"GRAVITY_PROP_WHITELIST_PASS count={props.Length} source=user_scene_models_only identities={string.Join(",", props.Select(p => p.displayName).Distinct())} allSevenRequired=true");
            var rigMotions = game.AliveEnemies.Select(e => e.GetComponent<RobotRigMotion>()).ToArray();
            if (!Check(rigMotions.All(r => r != null && r.HasRig), "robot rig motion/bone mapping missing")) yield break;
            var walkProbe = rigMotions[0];
            walkProbe.DebugSetPose(.25f, true, false);
            var walkAngleA = walkProbe.CurrentWalkBoneAngle;
            walkProbe.DebugSetPose(.75f, true, false);
            var walkAngleB = walkProbe.CurrentWalkBoneAngle;
            if (!Check(walkAngleA > 12f && walkAngleB > 12f && walkProbe.IsWalkAnimating, "walk pose did not drive bound leg/arm bones")) yield break;
            Debug.Log($"GRAVITY_RIG_WALK_PASS rig={walkProbe.name} phaseA={walkAngleA:0.0} phaseB={walkAngleB:0.0} rootMotion=ai_only");
            if (!Check(projectilePrefabs.All(e => e.projectilePrefab != null && e.projectilePrefab.GetComponent<EnemyProjectile>() != null), "26/28 projectile prefabs missing")) yield break;
            var distinctProjectilePrefabs = projectilePrefabs.SelectMany(e => new[] { e.projectilePrefab, e.alternateProjectilePrefab }).Where(prefab => prefab != null).Distinct().ToArray();
            if (!Check(distinctProjectilePrefabs.All(prefab =>
                    prefab.GetComponent<SphereCollider>() is { radius: >= .29f and <= .31f }
                    && prefab.GetComponent<Light>() is { intensity: >= 1.5f, range: >= 2.5f }
                    && prefab.GetComponent<TrailRenderer>() != null), "projectile size/collider/emissive glow configuration invalid")) yield break;
            Debug.Log($"GRAVITY_PROJECTILE_PACING_PASS wave={game.Wave} concurrent={game.ProjectileConcurrencyLimit} interval={game.EnemyShotInterval:0.000} glow=true size=0.70");

            var healthProbeObject = Instantiate(game.healthPackPrefab, game.playerHead.position + game.playerHead.forward * 3.4f - game.playerHead.up * .65f, Quaternion.identity);
            var healthBillboard = healthProbeObject.GetComponent<FacePlayerBillboard>();
            if (!Check(healthProbeObject.GetComponentsInChildren<ParticleSystem>(true).Any(system => system != null && system.gameObject.name == "VFX_绿色回血粒子_27"), "health pack 27 green particle system missing")) yield break;
            Debug.Log("GRAVITY_HEALTH_PARTICLE_PASS asset=27 greenLoop=true pickupBurst=true");
            yield return null;
            yield return new WaitForEndOfFrame();
            if (!Check(healthBillboard != null && healthBillboard.PlayerFacingDot > .98f, $"health pack 27 front did not face camera: dot={healthBillboard?.PlayerFacingDot:0.000}")) yield break;
            if (!Check(Mathf.Abs(Mathf.DeltaAngle(healthBillboard.yawOffset, 90f)) < .1f, "health pack 27 cross-marked model front is not using the calibrated side axis")) yield break;
            Debug.Log($"GRAVITY_HEALTH_RUNTIME_ASSERT_PASS asset=27 dot={healthBillboard.PlayerFacingDot:0.000} modelFront=negativeX yawOffset=90 frontTowardCamera=true intervalWaves=3");
            Destroy(healthProbeObject);
            if (!Check(projectilePrefabs.All(e => Mathf.Abs(Mathf.DeltaAngle(e.visualYawOffset, 180f)) < .1f && e.visualRoot != null && Mathf.Abs(Mathf.DeltaAngle(e.visualRoot.localEulerAngles.y, 180f)) < .1f), "robot visual yaw is not authoritative 180 degrees")) yield break;
            yield return new WaitForFixedUpdate();
            if (!Check(projectilePrefabs.All(e => Vector3.Dot(-e.transform.forward, Vector3.ProjectOnPlane(game.playerHead.position - e.transform.position, Vector3.up).normalized) > .75f), "robot logic facing axis does not face player")) yield break;
            if (!Check(projectilePrefabs.All(e => Vector3.Dot(e.VisualFacingDirection, Vector3.ProjectOnPlane(game.playerHead.position - e.transform.position, Vector3.up).normalized) > .75f), "robot visual face axis does not face player")) yield break;

            var heldFacingProbe = game.AliveEnemies[0];
            game.leftHand.DebugGrab(heldFacingProbe);
            yield return new WaitForSeconds(.45f);
            if (!Check(game.leftHand.gravityRay.enabled, "gravity beam did not appear for held target")) yield break;
            if (!Check(heldFacingProbe.HeldVisualScaleRatio <= .87f && heldFacingProbe.HeldVisualScaleRatio >= .81f, $"held enemy visual scale was not reduced smoothly: ratio={heldFacingProbe.HeldVisualScaleRatio:0.000}")) yield break;
            var heldToPlayer = Vector3.ProjectOnPlane(game.playerHead.position - heldFacingProbe.transform.position, Vector3.up).normalized;
            var heldLogic = heldFacingProbe.GetComponent<RobotEnemy>();
            var heldRig = heldFacingProbe.GetComponent<RobotRigMotion>();
            heldRig.DebugSetPose(.12f, false, true);
            var heldSwayA = heldRig.CurrentHeldVisualAngle;
            heldRig.DebugSetPose(.68f, false, true);
            var heldSwayB = heldRig.CurrentHeldVisualAngle;
            if (!Check(heldSwayA > 4f && heldSwayB > 4f && heldRig.IsHeldSwayAnimating, "held robot bone sway amplitude was not visible")) yield break;
            if (!Check(heldLogic != null && Vector3.Dot(heldLogic.VisualFacingDirection, heldToPlayer) > .95f, "held enemy turned side/back instead of preserving front view")) yield break;
            if (!Check(heldLogic.visualRoot != null && Mathf.Abs(Mathf.DeltaAngle(heldLogic.visualRoot.localEulerAngles.y, 180f)) < .1f, "held enemy visual node changed from authoritative Y=180")) yield break;
            Debug.Log($"GRAVITY_HELD_ROBOT_FACING_PASS dot={Vector3.Dot(heldLogic.VisualFacingDirection, heldToPlayer):0.000} visualLocalEuler={heldLogic.visualRoot.localEulerAngles} swayA={heldSwayA:0.0} swayB={heldSwayB:0.0} heldScaleRatio={heldFacingProbe.HeldVisualScaleRatio:0.000}");
            game.leftHand.ForceForget(heldFacingProbe);
            heldFacingProbe.Release(Vector3.zero, Vector3.zero);

            var defeated = game.AliveEnemies[0];
            defeated.DestroyTarget(DestroyReason.SmokeTest, false);
            if (!Check(!game.IsRegistered(defeated), "defeated enemy remained registered")) yield break;
            yield return new WaitForSeconds(.85f);
            if (!Check(RobotBreakApartEffect.LastCenterDissolvePassed && RobotBreakApartEffect.LastCenterYDrift < .03f, $"enemy death center drifted {RobotBreakApartEffect.LastCenterYDrift:0.0000}m")) yield break;
            if (!Check(defeated == null, "defeated enemy object/renderers/colliders remained in scene")) yield break;

            var heavy = game.NearestAvailableProp(game.leftHand.transform.position);
            if (heavy != null)
            {
                heavy.pullStagesRequired = 2;
                var firstStageHeld = heavy.RequestGrab(game.leftHand);
                if (!Check(!firstStageHeld && heavy.PullStagesCompleted == 1, "large prop did not require first pull stage")) yield break;
                heavy.DebugSetPullStage(1);
                var secondStageHeld = heavy.RequestGrab(game.leftHand);
                if (!Check(secondStageHeld && heavy.IsHeld, "large prop did not complete second pull stage")) yield break;
                heavy.Release(Vector3.zero, Vector3.zero);
            }

            var blocker = game.NearestAvailableProp(game.leftHand.transform.position);
            if (!Check(blocker != null, "no blocker prop available")) yield break;
            game.leftHand.DebugGrab(blocker);
            var blockProjectileObject = Instantiate(projectilePrefabs[0].projectilePrefab, blocker.Center, Quaternion.identity);
            var blockProjectile = blockProjectileObject.GetComponent<EnemyProjectile>();
            blockProjectile.Launch((game.playerHead.position - blocker.Center).normalized);
            if (!Check(game.TryBlockProjectile(blockProjectile), "held prop did not consume projectile block")) yield break;
            if (!Check(!game.IsRegistered(blocker), "consumed blocker remained registered")) yield break;
            yield return new WaitForSeconds(.65f);
            if (!Check(blocker == null, "consumed blocker renderer/collider/object remained")) yield break;

            for (var i = 0; i < 6 && game.AliveEnemies.Count > 0; i++)
            {
                var enemies = game.AliveEnemies.Where(e => e != null && e.IsAlive && !e.IsHeld).Take(2).ToArray();
                if (!Check(enemies.Length > 0, "no selectable enemy during wave clear")) yield break;
                game.leftHand.DebugGrab(enemies[0]);
                GravityTarget second = enemies.Length > 1 ? enemies[1] : game.NearestAvailableProp(game.rightHand.transform.position);
                if (!Check(second != null, "no collision partner for final enemy")) yield break;
                game.rightHand.DebugGrab(second);
                game.CollideHeld();
                yield return new WaitForSeconds(.5f);
            }

            foreach (var survivor in game.AliveEnemies.ToArray()) survivor.DestroyTarget(DestroyReason.SmokeTest, false);

            if (!Check(game.AliveEnemies.Count == 0, "wave 1 enemies remained after paired collision loop")) yield break;
            yield return new WaitForSeconds(4.1f);
            if (!Check(game.CompletedWaves == 1, "completed-wave score was not 1")) yield break;
            if (!Check(game.Wave == 2 && game.AliveEnemies.Count > 0, "wave 2 did not spawn")) yield break;
            var projectileProbe = Instantiate(projectilePrefabs[0].projectilePrefab, game.playerHead.position + game.playerHead.forward * 3f, Quaternion.identity);
            var projectileLogic = projectileProbe.GetComponent<EnemyProjectile>();
            projectileLogic.Launch(-game.playerHead.forward);
            if (!Check(projectileLogic.VelocityTargetDot > .98f && projectileLogic.VisualVelocityDot > .98f && projectileLogic.CameraNoseDot > .98f, "projectile launch direction/real nose axis mismatch")) yield break;
            game.Repulse();
            if (!Check(game.RepulseCooldown > 3f, "repulse cooldown did not start")) yield break;
            yield return null;
            if (!Check(projectileLogic.Target.ThrownUntil > Time.time, "repulse did not reflect projectile")) yield break;
            if (!Check(projectileLogic.VisualVelocityDot > .98f && projectileLogic.CameraNoseDot < -.98f, "reflected projectile nose did not flip naturally to new velocity")) yield break;
            projectileLogic.Target.DestroyTarget(DestroyReason.SmokeTest, false);

            var beforeChain = game.AliveEnemies.Count;
            var canister = FindAnyObjectByType<ChainReactionProp>();
            if (canister != null)
            {
                canister.transform.position = game.AliveEnemies[0].transform.position;
                Physics.SyncTransforms();
                canister.GetComponent<GravityTarget>().DestroyTarget(DestroyReason.ChainReaction, false);
                yield return null;
                if (!Check(game.AliveEnemies.Count < beforeChain, "environment chain reaction did not damage an enemy")) yield break;
            }
            else Debug.Log("GRAVITY_ENVIRONMENT_CHAIN_SKIPPED reason=no_user_scene_explosive_model synthetic_canister_forbidden=true");

            game.ActivateUltimate();
            if (!Check(game.UltimateRequirement == 220 && game.UltimateCharge == 0f, "ultimate cost did not scale after activation")) yield break;
            yield return new WaitForSeconds(4.8f);
            if (!Check(game.AliveEnemies.Count == 0, "ultimate did not clear the remaining wave")) yield break;
            yield return new WaitForSeconds(4.1f);
            if (!Check(game.Wave == 3 && game.CompletedWaves == 2 && game.AliveEnemies.Count > 0, "infinite wave flow did not continue after ultimate")) yield break;
            Debug.Log($"GRAVITY_DESKTOP_SMOKE_PASS wave={game.Wave} completedWaves={game.CompletedWaves} score={game.Score} enemies={game.AliveEnemies.Count} shield={game.Shield:0} cooldown={game.RepulseCooldown:0.0} ultimateRequirement={game.UltimateRequirement}");
            Application.Quit(0);
        }

        private static bool Check(bool condition, string message)
        {
            if (condition) return true;
            Debug.LogError("GRAVITY_DESKTOP_SMOKE_FAIL " + message);
            Application.Quit(2);
            return false;
        }
    }
}
