using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace GravityRepulsion
{
    /// <summary>Opt-in regression for actor paths that bypass ordinary solid-body response.</summary>
    public sealed class GravityActorWallSmoke : MonoBehaviour
    {
        private readonly WaitForFixedUpdate physicsStep = new();
        private readonly WaitForEndOfFrame renderedFrame = new();
        private GravityGameManager game;
        private BoxCollider wall;
        private bool failed;
        private int checks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (!Environment.GetCommandLineArgs().Contains("-gravityActorWallSmoke")
                || FindFirstObjectByType<GravityActorWallSmoke>() != null) return;
            var go = new GameObject("REGRESSION_ONLY_ActorWalls0910");
            DontDestroyOnLoad(go);
            go.AddComponent<GravityActorWallSmoke>();
        }

        private IEnumerator Start()
        {
            Application.runInBackground = true;
            yield return new WaitForSecondsRealtime(2.5f);
            FindFirstObjectByType<GravityTitleController>()?.StartGame();
            yield return new WaitForSecondsRealtime(6f);
            game = FindFirstObjectByType<GravityGameManager>();
            if (!Check(game != null && GravityGroundSurface.Active != null, "game_ready")) yield break;
            game.StopAllCoroutines();
            game.enabled = false;
            foreach (var hand in FindObjectsByType<GravityHand>(FindObjectsSortMode.None)) hand.enabled = false;
            foreach (var desktop in FindObjectsByType<DesktopFallbackController>(FindObjectsSortMode.None)) desktop.enabled = false;
            foreach (var target in FindObjectsByType<GravityTarget>(FindObjectsSortMode.None)) target.gameObject.SetActive(false);

            // A facade in an empty part of the real foundation isolates all scripted actor
            // paths. The separate architecture smoke audits the authored houses themselves.
            var wallObject = new GameObject("REGRESSION_ONLY_ACTOR_WALL");
            wallObject.transform.position = new Vector3(-85f, 50.1f, -75f);
            wall = wallObject.AddComponent<BoxCollider>();
            wall.size = new Vector3(2f, 100f, 30f);
            wallObject.AddComponent<GravityArchitectureSolid>();
            Physics.SyncTransforms();

            var prefabs = new[] { game.scoutRobotPrefab, game.engineerRobotPrefab, game.guardRobotPrefab };
            foreach (var prefab in prefabs)
            {
                game.playerHead.SetPositionAndRotation(new Vector3(-96f, 1.6f, -75f), Quaternion.LookRotation(Vector3.right));
                var robot = Instantiate(prefab, new Vector3(-79f, .13f, -75f), RobotEnemy.RootRotationFacing(Vector3.left));
                var actor = robot.GetComponent<RobotEnemy>();
                var target = robot.GetComponent<GravityTarget>();
                actor.projectilePrefab = null;
                actor.moveSpeed = 8f;
                GravityGroundSurface.Active.ResolvePenetration(target);
                for (float elapsed = 0f; elapsed < 1.6f; elapsed += Time.fixedDeltaTime)
                {
                    yield return physicsStep;
                    if (!Check(Outside(target), "walking_visible_shape_" + prefab.name)) yield break;
                }
                if (!Check(target.transform.position.x < -80f && target.WorldBounds.min.x >= wall.bounds.max.x - .035f,
                        "walking_reached_and_stopped_" + prefab.name)) yield break;
                robot.SetActive(false);
                Destroy(robot);
            }

            // Force the real wave spawner to start inside the facade, then inspect its result.
            var spawn = new GameObject("REGRESSION_ONLY_BLOCKED_SPAWN").transform;
            spawn.position = new Vector3(-85f, .13f, -75f);
            var originalSpawns = game.spawnPoints;
            game.spawnPoints = new[] { spawn };
            typeof(GravityGameManager).GetMethod("SpawnWave", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(game, new object[] { 3 });
            var spawnedTargets = game.AliveEnemies.ToArray();
            foreach (var target in spawnedTargets)
                Debug.Log($"GRAVITY_ACTOR_SPAWN_BEFORE_SYNC count={spawnedTargets.Length} {Describe(target)}");
            // SpawnWave resolves the new rigidbody before its first simulation step; publish
            // those authored transforms before reading the engine's cached collider bounds.
            Physics.SyncTransforms();
            foreach (var target in spawnedTargets)
                Debug.Log($"GRAVITY_ACTOR_SPAWN_AFTER_SYNC count={spawnedTargets.Length} {Describe(target)}");
            if (!Check(spawnedTargets.Length == 3 && spawnedTargets.All(Outside), "real_wave_spawn_resolves_house")) yield break;
            game.spawnPoints = originalSpawns;
            foreach (var target in spawnedTargets) { target.gameObject.SetActive(false); Destroy(target.gameObject); }
            Destroy(spawn.gameObject);

            // A billboard can rotate its wide visual into a wall despite a stationary body.
            var heal = Instantiate(game.healthPackPrefab, new Vector3(-80f, 1.6f, -75f), Quaternion.identity);
            var pack = heal.GetComponent<GravityTarget>();
            yield return renderedFrame;
            heal.transform.position += Vector3.left * (pack.WorldBounds.min.x - wall.bounds.max.x - .02f);
            pack.Body.position = heal.transform.position;
            for (int turn = 0; turn < 24; turn++)
            {
                float angle = turn * Mathf.PI / 12f;
                game.playerHead.position = pack.Center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 5f;
                yield return renderedFrame;
                if (!Check(Outside(pack), "billboard_rotation_" + turn)) yield break;
            }
            heal.transform.position = new Vector3(-77f, 2f, -75f);
            pack.Body.position = heal.transform.position;
            pack.ActivatePhysicsForInteraction(false);
            pack.Body.linearVelocity = Vector3.left * 40f;
            for (float elapsed = 0f; elapsed < .6f; elapsed += Time.fixedDeltaTime)
            {
                yield return physicsStep;
                if (!Check(pack.IsAlive && Outside(pack), "heal_throw_stops_at_wall")) yield break;
            }
            if (!Check(pack.Body.linearVelocity.x > -.1f, "heal_throw_inward_velocity_removed")) yield break;
            heal.SetActive(false);
            Destroy(heal);

            // Trigger missiles must stop on the swept path, including reflection.
            var launcher = game.scoutRobotPrefab.GetComponent<RobotEnemy>();
            var shots = new[] { launcher.projectilePrefab, launcher.alternateProjectilePrefab };
            foreach (var prefab in shots)
            {
                if (!Check(prefab != null, "projectile_prefab_present")) yield break;
                foreach (bool reflected in new[] { false, true })
                {
                    var missileObject = Instantiate(prefab, new Vector3(-77f, 3f, -75f), Quaternion.identity);
                    var missile = missileObject.GetComponent<EnemyProjectile>();
                    missile.speed = 80f;
                    missile.Launch(Vector3.left);
                    if (reflected) missile.Reflect(Vector3.left);
                    for (float elapsed = 0f; elapsed < 1f && missile != null && missile.Target.IsAlive; elapsed += Time.fixedDeltaTime)
                    {
                        yield return physicsStep;
                        if (missile != null && missile.Target.IsAlive
                            && !Check(Outside(missile.Target), "missile_sweep_" + prefab.name)) yield break;
                    }
                    if (!Check(missile == null || !missile.Target.IsAlive,
                            "missile_stops_at_wall_" + prefab.name + "_reflected=" + reflected)) yield break;
                    if (missileObject != null) Destroy(missileObject);
                }
            }

            // Place the real singularity centre inside the facade to stress every orbit phase.
            game.playerHead.SetPositionAndRotation(new Vector3(-98f, 1.6f, -75f), Quaternion.LookRotation(Vector3.right));
            foreach (var prefab in prefabs)
            {
                var robot = Instantiate(prefab, new Vector3(-79f, .13f, -75f), Quaternion.identity);
                robot.GetComponent<RobotEnemy>().projectilePrefab = null;
            }
            game.DebugReadyUltimate();
            game.ActivateUltimate();
            if (!Check(game.UltimateActive, "real_ultimate_started")) yield break;
            int orbitFrames = 0;
            for (float elapsed = 0f; elapsed < 6f && game.UltimateActive; elapsed += Time.deltaTime)
            {
                yield return renderedFrame;
                orbitFrames++;
                foreach (var target in game.AliveEnemies)
                    if (!Check(Outside(target), "ultimate_visible_shape_" + target.name)) yield break;
            }
            if (!Check(!game.UltimateActive && game.AliveEnemies.Count == 0 && orbitFrames > 10,
                    "real_ultimate_completed")) yield break;
            Debug.Log($"GRAVITY_ACTOR_WALL_SMOKE_PASS checks={checks} robots=3 walking=true blockedSpawn=3 healRotations=24 healThrowSpeed=40 projectileTypes=2 projectileRuns=4 maxShotSpeed=80 realUltimate=true orbitFrames={orbitFrames}");
            Application.Quit(0);
        }

        private bool Outside(GravityTarget target)
        {
            if (target == null || !target.IsAlive) return true;
            var shape = target.WorldBounds;
            var facade = wall.bounds;
            const float tolerance = .035f;
            return shape.max.x <= facade.min.x + tolerance || shape.min.x >= facade.max.x - tolerance
                || shape.max.y <= facade.min.y + tolerance || shape.min.y >= facade.max.y - tolerance
                || shape.max.z <= facade.min.z + tolerance || shape.min.z >= facade.max.z - tolerance;
        }

        private string Describe(GravityTarget target)
        {
            if (target == null) return "target=null";
            var shape = target.WorldBounds;
            return $"target={target.name} active={target.gameObject.activeInHierarchy} alive={target.IsAlive} root={target.transform.position:F4} body={target.Body.position:F4} min={shape.min:F4} max={shape.max:F4} wallMin={wall.bounds.min:F4} wallMax={wall.bounds.max:F4} outside={Outside(target)} penetration={GravityArchitectureCollision.HasPenetration(target)}";
        }

        private bool Check(bool condition, string reason)
        {
            if (condition) { checks++; return true; }
            if (!failed) Debug.LogError("GRAVITY_ACTOR_WALL_SMOKE_FAIL " + reason);
            failed = true;
            Application.Quit(3);
            return false;
        }
    }
}
