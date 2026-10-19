using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FortStages.Editor
{
    /// <summary>Opt-in checks that modify only the running scene and restore saved preferences.</summary>
    public static class FortCombatPlayChecks
    {
        private sealed class Step
        {
            public string name;
            public double delay;
            public bool realTime;
            public Func<bool> ready;
            public Action action;
        }

        private sealed class SavedPref
        {
            public string key;
            public int type;
            public bool existed;
            public int intValue;
            public float floatValue;
            public string stringValue;
        }

        public static string Status { get; private set; } = "Idle";
        public static string Report => string.Join("\n", results);
        private static readonly List<string> results = new List<string>();
        private static readonly Queue<Step> steps = new Queue<Step>();
        private static readonly List<SavedPref> preferences = new List<SavedPref>();
        private static bool running;
        private static bool oldRunInBackground;
        private static UnityEngine.Random.State oldRandomState;
        private static double began;
        private static double stepRealStart;
        private static float stepGameStart;
        private static PlayerController2 player;
        private static PlayerSkills skills;
        private static Enemy cactus;
        private static FortThrownProjectile cactusProjectile;
        private static FortThrownProjectile poisonProjectile;
        private static FortThrownProjectile shot;
        private static GameObject wall;
        private static GameObject extraCollider;
        private static Vector3 pausedPosition;
        private static Vector3 pursuitStart;
        private static string savedSkills;

        [MenuItem("Tools/Fort Stages/Run Combat Play Checks")]
        public static void RunAll()
        {
            if (running) return;
            results.Clear();
            if (!Application.isPlaying)
            {
                Status = "Failed: RunAll requires Play mode.";
                results.Add(Status);
                return;
            }

            steps.Clear();
            preferences.Clear();
            foreach (string key in new[] { "NextStage", "Carry_HasData" }) Snapshot(key, 0);
            foreach (string key in new[] { "Carry_Hp", "Carry_MaxHp", "Carry_Speed", "Carry_Attack", "Carry_Reload", "Carry_CameraY", "BestTime_Stage6" }) Snapshot(key, 1);
            Snapshot("Carry_Skills", 2);
            oldRunInBackground = Application.runInBackground;
            oldRandomState = UnityEngine.Random.state;
            Application.runInBackground = true;
            running = true;
            began = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
            try
            {
                PlayerPrefs.SetInt("Carry_HasData", 0);
                PlayerPrefs.SetInt("NextStage", 6);
                Time.timeScale = 1f;
                SceneManager.LoadScene("Stage6");
                Queue("fresh Stage6 countdown", Prepare, ready: () =>
                {
                    var manager = Object.FindFirstObjectByType<GameManager>();
                    return SceneManager.GetActiveScene().name == "Stage6" && manager != null && manager.CanPause;
                });
                ArmStep();
            }
            catch (Exception exception) { Finish(false, exception.GetBaseException().Message); }
        }

        private static void Queue(string name, Action action, double delay = 0, bool realTime = false, Func<bool> ready = null)
        {
            steps.Enqueue(new Step { name = name, action = action, delay = delay, realTime = realTime, ready = ready });
        }

        private static void ArmStep()
        {
            stepRealStart = EditorApplication.timeSinceStartup;
            stepGameStart = Time.time;
            if (steps.Count > 0) Status = "Running: " + steps.Peek().name;
        }

        private static void Tick()
        {
            if (!running) return;
            if (!Application.isPlaying) { Finish(false, "Play mode stopped."); return; }
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - began > 180)
            {
                Finish(false, "Checks exceeded 180 seconds.");
                return;
            }
            if (steps.Count == 0) { Finish(true, "All combat checks passed."); return; }
            Step step = steps.Peek();
            try
            {
                double elapsed = step.realTime ? EditorApplication.timeSinceStartup - stepRealStart : Time.time - stepGameStart;
                if (EditorApplication.timeSinceStartup - stepRealStart > Math.Max(20, step.delay + 15))
                    throw new InvalidOperationException("Step timed out.");
                if (elapsed < step.delay || (step.ready != null && !step.ready())) return;
                steps.Dequeue();
                step.action();
                if (running) ArmStep();
            }
            catch (Exception exception) { Finish(false, step.name + ": " + exception.GetBaseException().Message); }
        }

        private static void Prepare()
        {
            player = Object.FindFirstObjectByType<PlayerController2>();
            Require(player != null, "Player missing.");
            skills = player.GetComponent<PlayerSkills>();
            Require(skills != null && skills.skills.Length == 11, "Expected eleven skills after scene-array migration.");
            Require(!string.IsNullOrEmpty(skills.skills[2].skillName) && !string.IsNullOrEmpty(skills.skills[10].skillName), "Healing skill labels missing.");
            player.maxHp = 4f;
            player.HealToFull();
            player.GetComponent<StarterAssets.StarterAssetsInputs>().move = Vector2.zero;
            Rigidbody playerBody = player.GetComponent<Rigidbody>();
            playerBody.linearVelocity = playerBody.angularVelocity = Vector3.zero;
            playerBody.constraints = RigidbodyConstraints.FreezeAll;
            playerBody.position = new Vector3(0f, 0.05f, -8f);
            player.transform.position = playerBody.position;

            foreach (Enemy enemy in Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None))
            {
                enemy.enabled = false;
                enemy.attackDamage = 0f;
                Rigidbody body = enemy.GetComponent<Rigidbody>();
                if (body != null)
                {
                    body.linearVelocity = body.angularVelocity = Vector3.zero;
                    body.constraints = RigidbodyConstraints.FreezeAll;
                }
                var candidate = enemy;
                if (candidate.usesRangedAttack && candidate.projectilePrefab != null && !candidate.projectilePrefab.poisonous)
                {
                    cactus = enemy;
                }
            }
            foreach (FortTrap trap in Object.FindObjectsByType<FortTrap>(FindObjectsSortMode.None)) trap.enabled = false;
            foreach (TreasureChest chest in Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None))
                foreach (Collider collider in chest.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (FortThrownProjectile projectile in Object.FindObjectsByType<FortThrownProjectile>(FindObjectsSortMode.None)) Object.Destroy(projectile.gameObject);

            Require(cactus != null && cactus.usesRangedAttack && cactus.projectilePrefab != null, "Stage6 cactus has no ranged projectile.");
            cactusProjectile = cactus.projectilePrefab;
            GameObject beholder = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BeholderArcher.prefab");
            Require(beholder != null && beholder.GetComponent<Enemy>() != null && beholder.GetComponent<Enemy>().usesRangedAttack, "Beholder ranged component missing.");
            poisonProjectile = beholder.GetComponent<Enemy>().projectilePrefab;
            Require(poisonProjectile != null && poisonProjectile.poisonous, "Beholder projectile is not poisonous.");
            Physics.SyncTransforms();
            QueueMovementChecks();
            QueueProjectileChecks();
            QueuePoisonChecks();
            QueueSkillChecks();
        }

        private static void QueueMovementChecks()
        {
            Queue("cactus starts beyond stopping distance", () =>
            {
                Rigidbody body = cactus.GetComponent<Rigidbody>();
                body.position = new Vector3(0f, 0.05f, -15f);
                cactus.transform.position = body.position;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                cactus.player = player.transform;
                cactus.enabled = true;
                pursuitStart = body.position;
                Physics.SyncTransforms();
            }, 0.05);
            Queue("existing Enemy pursuit", () =>
                Require(cactus.transform.position.z > pursuitStart.z + 0.3f, "Cactus did not approach outside stopping distance."), 0.3);
            Queue("cactus stops to throw", () =>
            {
                Require(Vector3.ProjectOnPlane(cactus.GetComponent<Rigidbody>().linearVelocity, Vector3.up).sqrMagnitude < 0.01f, "Cactus did not stop inside ranged distance.");
                Require(Mathf.Approximately(cactus.attackDamage, 0f), "Ranged mob still deals contact damage.");
            }, 0.5);
            Queue("autonomous cactus throw and existing knockback", () =>
            {
                Equal(player.hp, 3f, "Autonomous cactus throw damage");
                Require(!cactus.TakeDamage(0.5f) && Mathf.Approximately(cactus.hp, 0.5f), "Existing Enemy health did not accept half damage.");
                cactus.Knockback(Vector3.right);
                Require(cactus.GetComponent<Rigidbody>().linearVelocity.x > 20f, "Ranged controller suppressed existing knockback.");
            }, ready: () => player.hp <= 3f);
            Queue("isolate completed movement check", () =>
            {
                cactus.enabled = false;
                cactus.attackDamage = 0f;
                Rigidbody body = cactus.GetComponent<Rigidbody>();
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.constraints = RigidbodyConstraints.FreezeAll;
                body.position = new Vector3(-7f, 0.05f, 3f);
                cactus.transform.position = body.position;
                Physics.SyncTransforms();
                results.Add("Cactus inherits Enemy approach, half-HP damage and knockback; autonomously stops and throws for 1 HP with contact damage suppressed.");
            }, 0.2);
        }

        private static Vector3 HitPoint => player.transform.position + Vector3.up * 0.6f;

        private static FortThrownProjectile Launch(FortThrownProjectile prefab, Vector3 position, Vector3 direction)
        {
            var projectile = Object.Instantiate(prefab, position, Quaternion.identity);
            projectile.Launch(cactus, player, direction);
            return projectile;
        }

        private static void QueueProjectileChecks()
        {
            Queue("cactus physical projectile", () => { player.HealToFull(); shot = Launch(cactusProjectile, HitPoint - Vector3.forward * 3f, Vector3.forward); });
            Queue("one HP projectile impact", () =>
            {
                Equal(player.hp, 3f, "One cactus projectile damage");
                Require(shot == null, "Cactus projectile survived impact.");
            }, 0.6);
            Queue("projectile cannot hit twice", () => Equal(player.hp, 3f, "Repeated impact"), 0.4);
            Queue("solid wall interception", () =>
            {
                player.HealToFull();
                wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.name = "CombatCheckWall";
                wall.layer = LayerMask.NameToLayer("Obstacle");
                wall.transform.position = HitPoint - Vector3.forward * 1.5f;
                wall.transform.localScale = new Vector3(3f, 2f, 0.2f);
                Physics.SyncTransforms();
                shot = Launch(cactusProjectile, HitPoint - Vector3.forward * 3f, Vector3.forward);
            });
            Queue("wall absorbed projectile", () =>
            {
                Equal(player.hp, 4f, "Wall must block damage");
                Require(shot == null, "Projectile did not collide with wall.");
                Object.Destroy(wall);
                wall = null;
            }, 0.6);
            Queue("overlap with multiple player colliders", () =>
            {
                extraCollider = new GameObject("CombatCheckExtraCollider");
                extraCollider.transform.SetParent(player.transform, false);
                extraCollider.transform.localPosition = Vector3.up * 0.6f;
                extraCollider.AddComponent<SphereCollider>().radius = 0.35f;
                Physics.SyncTransforms();
                shot = Launch(cactusProjectile, HitPoint, Vector3.forward);
            }, 0.05);
            Queue("overlap is exactly one hit", () =>
            {
                Equal(player.hp, 3f, "Multiple collider overlap damage");
                Require(shot == null, "Overlap projectile survived impact.");
                Object.Destroy(extraCollider);
                extraCollider = null;
                results.Add("Actual projectile prefab deals exactly 1 HP, hits once across duplicate colliders, and is blocked by a solid wall.");
            }, 0.2);
        }

        private static void QueuePoisonChecks()
        {
            Queue("beholder poison impact", () => { player.HealToFull(); shot = Launch(poisonProjectile, HitPoint, Vector3.forward); }, 0.05);
            Queue("poison has no immediate direct damage", () =>
            {
                Equal(player.hp, 4f, "Poison immediate damage");
                Require(shot == null && player.GetComponent<PlayerHealthEffects>() != null, "Poison projectile did not apply its effect.");
                shot = Launch(cactusProjectile, HitPoint - Vector3.forward * 3f, Vector3.forward);
                pausedPosition = shot.transform.position;
                Time.timeScale = 0f;
            }, 0.15);
            Queue("pause freezes poison and projectile", () =>
            {
                Equal(player.hp, 4f, "Poison advanced while paused");
                Require(shot != null && (shot.transform.position - pausedPosition).sqrMagnitude < 0.00001f, "Projectile moved or expired while paused.");
                Object.Destroy(shot.gameObject);
                shot = null;
                Time.timeScale = 1f;
            }, 1.2, true);
            for (int i = 1; i <= 4; i++)
            {
                float expected = 4f - i * 0.5f;
                Queue("poison half-HP tick " + i, () => Equal(player.hp, expected, "Poison tick"), ready: () => player.hp <= expected);
            }
            Queue("poison total is capped at two HP", () =>
            {
                Equal(player.hp, 2f, "Poison extra tick");
                results.Add("Beholder poison has no direct hit damage, pauses correctly, and deals 0.5 HP four times for exactly 2 HP.");
            }, 1.2);
        }

        private static void Select(int id, bool verifyRandomDraw = false)
        {
            var choicesField = typeof(PlayerSkills).GetField("currentSkillChoices", BindingFlags.Instance | BindingFlags.NonPublic);
            Require(choicesField != null, "Skill choice field not found.");
            if (verifyRandomDraw)
            {
                UnityEngine.Random.InitState(1261012);
                for (int attempt = 0; attempt < 200; attempt++)
                {
                    skills.OpenSkillSelection(null);
                    int[] choices = (int[])choicesField.GetValue(skills);
                    int slot = Array.IndexOf(choices, id);
                    if (slot < 0) continue;
                    skills.SelectSkill(slot);
                    return;
                }
                throw new InvalidOperationException("New healing skill was absent from 200 real random selections.");
            }
            skills.OpenSkillSelection(null);
            choicesField.SetValue(skills, new[] { id, -1, -1 });
            skills.SelectSkill(0);
        }

        private static void CheckHeart(int index, float fillAmount, float capacity)
        {
            Image[] hearts = player.heartParent.GetComponentsInChildren<Image>(true)
                .Where(image => image.transform.parent == player.heartParent && image.gameObject.activeSelf).ToArray();
            Require(index < hearts.Length, "Expected heart is missing: " + index);
            Image heart = hearts[index];
            Transform fillTransform = heart.transform.Find("HalfHeartFill");
            Require(fillTransform != null, "Half-heart fill missing.");
            Image fill = fillTransform.GetComponent<Image>();
            Require(fill != null && fill.type == Image.Type.Filled && fill.fillMethod == Image.FillMethod.Horizontal, "Half-heart rendering mode incorrect.");
            Equal(fill.fillAmount, fillAmount, "Heart fill " + index);
            Equal(heart.fillAmount, capacity, "Heart capacity " + index);
            Require(heart.GetComponentsInChildren<Image>(true).Count(image => image.name == "HalfHeartFill") == 1, "Heart contains duplicate fill children.");
        }

        private static void QueueSkillChecks()
        {
            Queue("real random selection of gradual healing", () =>
            {
                player.hp = 1f;
                player.TakeDamage(0f);
                Select(10, true);
                Equal(player.hp, 1f, "Gradual heal must not be immediate");
                Require(Time.timeScale == 1f && !skills.skillUI.activeSelf, "Skill selection did not resume play.");
            });
            Queue("healing pause", () => Time.timeScale = 0f, 0.1);
            Queue("healing remains frozen", () => { Equal(player.hp, 1f, "Healing advanced while paused"); Time.timeScale = 1f; }, 1.2, true);
            for (int i = 1; i <= 5; i++)
            {
                float expected = 1f + i * 0.5f;
                Queue("gradual heal half-HP tick " + i, () => Equal(player.hp, expected, "Healing tick"), ready: () => player.hp >= expected);
            }
            Queue("healing total and half-heart", () =>
            {
                Equal(player.hp, 3.5f, "Gradual heal total");
                CheckHeart(0, 1f, 1f);
                CheckHeart(3, 0.5f, 1f);
                player.TakeDamage(3f);
                CheckHeart(0, 0.5f, 1f);
                CheckHeart(1, 0f, 1f);
                player.AddMaxHp(0.5f);
                player.HealToFull();
                CheckHeart(4, 0.5f, 0.5f);
                player.maxHp = 4f;
                player.HealToFull();
                player.TakeDamage(0.5f);
                Select(10);
            }, 1.2);
            Queue("gradual heal respects max HP", () =>
            {
                Equal(player.hp, 4f, "Healing exceeded maximum HP");
                player.TakeDamage(3f);
                Select(2);
                Equal(player.hp, 4f, "Existing full-heal skill changed");
                results.Add("Skill 10 appears in real draws; selected healing ticks 0.5 five times, pauses, caps at max HP, and existing full heal stays immediate.");
                results.Add("Heart UI renders 3.5 HP, 0.5 HP and a new half-capacity fifth heart without duplicate fills.");
            }, 5.3);
            Queue("restore old and new saved skill IDs", () =>
            {
                player.TakeDamage(3f);
                skills.RestoreAcquiredSkills("0,0,3,9,9,10,10");
                savedSkills = skills.GetAcquiredSkillsString();
                string[] ids = savedSkills.Split(',');
                Require(ids.Count(id => id == "0") == 2 && ids.Count(id => id == "3") == 1 &&
                    ids.Count(id => id == "9") == 2 && ids.Count(id => id == "10") == 2 && ids.Count(id => id == "2") == 1,
                    "Old or appended skill ID counts were not preserved.");
                skills.RestoreAcquiredSkills("0,0,3,9,9,10,10");
                Require(skills.GetAcquiredSkillsString() == savedSkills, "Restoring the same skills twice duplicates their counts.");
                Equal(player.hp, 1f, "Restore replayed a healing skill");
            });
            Queue("restored gradual heal does not replay", () =>
            {
                Equal(player.hp, 1f, "Restore restarted gradual healing");
                results.Add("Saved IDs 0-9 retain their meanings; ID 10 round-trips idempotently and does not grant another heal on restore.");
                player.TakeDamage(0.5f);
                var carrier = player.GetComponent<PlayerStatusCarrier>();
                Require(carrier != null, "Player status carrier is missing.");
                carrier.Save();
                Equal(PlayerPrefs.GetFloat("Carry_Hp", -1f), 0.5f, "Actual carry-over save lost half HP");
                Require(PlayerPrefs.GetString("Carry_Skills", "") == savedSkills, "Actual carry-over save changed skill IDs.");
                results.Add("Existing PlayerStatusCarrier.Save preserves 0.5 HP and the complete old/new skill ID string.");
            }, 1.3);
        }

        private static void Snapshot(string key, int type)
        {
            preferences.Add(new SavedPref { key = key, type = type, existed = PlayerPrefs.HasKey(key),
                intValue = type == 0 ? PlayerPrefs.GetInt(key) : 0,
                floatValue = type == 1 ? PlayerPrefs.GetFloat(key) : 0f,
                stringValue = type == 2 ? PlayerPrefs.GetString(key) : null });
        }

        private static void Equal(float actual, float expected, string label)
        {
            Require(Mathf.Abs(actual - expected) < 0.001f, label + ": expected " + expected + ", got " + actual);
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && running) Finish(false, "Play mode stopped before completion.");
        }

        private static void OnAssemblyReload()
        {
            if (running) Finish(false, "Assembly reloaded before completion.");
        }

        private static void Finish(bool success, string message)
        {
            if (!running) return;
            running = false;
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
            var cleanupErrors = new List<string>();
            foreach (SavedPref pref in preferences)
            {
                try
                {
                    if (!pref.existed) PlayerPrefs.DeleteKey(pref.key);
                    else if (pref.type == 0) PlayerPrefs.SetInt(pref.key, pref.intValue);
                    else if (pref.type == 1) PlayerPrefs.SetFloat(pref.key, pref.floatValue);
                    else PlayerPrefs.SetString(pref.key, pref.stringValue);
                }
                catch (Exception exception) { cleanupErrors.Add(pref.key + ": " + exception.Message); }
            }
            try { PlayerPrefs.Save(); } catch (Exception exception) { cleanupErrors.Add(exception.Message); }
            Application.runInBackground = oldRunInBackground;
            UnityEngine.Random.state = oldRandomState;
            // Freeze the inspected runtime scene so later poison cannot trigger GameOver and rewrite restored preferences.
            if (Application.isPlaying) Time.timeScale = 0f;
            steps.Clear();
            if (cleanupErrors.Count > 0) { success = false; message += " Cleanup: " + string.Join("; ", cleanupErrors); }
            Status = (success ? "Passed: " : "Failed: ") + message;
            results.Add(Status + " (" + (EditorApplication.timeSinceStartup - began).ToString("F1") + "s)");
        }
    }
}
