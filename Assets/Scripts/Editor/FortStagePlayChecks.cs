using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using StarterAssets;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FortStages.Editor
{
    /// <summary>Reproducible integration checks against the actual stage and lobby scenes.</summary>
    public static class FortStagePlayChecks
    {
        private enum PrefType { Int, Float, String }
        private sealed class SavedPref
        {
            public string Key;
            public PrefType Type;
            public bool Existed;
            public int IntValue;
            public float FloatValue;
            public string StringValue;
        }
        private sealed class Step
        {
            public double Delay;
            public string Label;
            public Action Action;
        }

        public static string Status { get; private set; } = "Idle";
        public static readonly List<string> Results = new List<string>();
        public static string Report => string.Join("\n", Results);

        private static readonly Queue<Step> steps = new Queue<Step>();
        private static readonly List<SavedPref> savedPrefs = new List<SavedPref>();
        private static readonly List<Action> runtimeRestores = new List<Action>();
        private static bool running;
        private static bool oldRunInBackground;
        private static double due;
        private static double began;
        private static int currentStage;
        private static GameManager manager;
        private static PlayerController2 player;

        [MenuItem("Tools/Fort Stages/Run Play Checks")]
        public static void RunAll()
        {
            if (running) return;
            Results.Clear();
            if (!Application.isPlaying)
            {
                Status = "Failed: RunAll requires an existing Play mode session.";
                Results.Add(Status);
                return;
            }

            steps.Clear();
            runtimeRestores.Clear();
            SnapshotPreferences();
            oldRunInBackground = Application.runInBackground;
            Application.runInBackground = true;
            began = EditorApplication.timeSinceStartup;
            running = true;
            Status = "Running: loading fresh Stage6";
            EditorApplication.update += Tick;
            EditorApplication.playModeStateChanged += OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload += OnAssemblyReload;
            try
            {
                // Prevent the user's saved character build from changing test movement or health.
                PlayerPrefs.SetInt("Carry_HasData", 0);
                PlayerPrefs.SetInt("NextStage", 6);
                SceneManager.LoadScene("Stage6");
                double countdownDeadline = EditorApplication.timeSinceStartup + 15;
                Queue(4.4, "Stage6 countdown", () => WaitForStageStart(6, countdownDeadline));
                due = EditorApplication.timeSinceStartup + steps.Peek().Delay;
            }
            catch (Exception exception) { Finish(false, exception.Message); }
        }

        private static void Queue(double delay, string label, Action action)
        {
            steps.Enqueue(new Step { Delay = delay, Label = label, Action = action });
        }

        private static void Tick()
        {
            if (!running) return;
            if (!Application.isPlaying) { Finish(false, "Play mode stopped."); return; }
            EditorApplication.QueuePlayerLoopUpdate();
            if (EditorApplication.timeSinceStartup - began > 120)
            {
                Finish(false, "Integration check exceeded 120 seconds.");
                return;
            }
            if (steps.Count == 0 || EditorApplication.timeSinceStartup < due) return;
            Step step = steps.Dequeue();
            Status = "Running: " + step.Label;
            try
            {
                step.Action();
                if (running && steps.Count > 0)
                    due = EditorApplication.timeSinceStartup + steps.Peek().Delay;
            }
            catch (Exception exception)
            {
                Finish(false, step.Label + ": " + exception.GetBaseException().Message);
            }
        }

        private static void WaitForStageStart(int stage, double deadline)
        {
            Require(SceneManager.GetActiveScene().name == "Stage" + stage, "Real next-stage scene transition failed.");
            var stageManager = Object.FindFirstObjectByType<GameManager>();
            Require(stageManager != null, "Stage GameManager is missing.");
            if (stageManager.CanPause)
            {
                PrepareStage(stage);
                return;
            }
            Require(!ReadField<bool>(stageManager, "isGameStarted"), "Stage is blocked after its countdown.");
            Require(EditorApplication.timeSinceStartup < deadline, "Stage countdown did not finish.");
            Queue(0.1, "Stage" + stage + " countdown", () => WaitForStageStart(stage, deadline));
        }

        private static void PrepareStage(int stage)
        {
            currentStage = stage;
            Require(SceneManager.GetActiveScene().name == "Stage" + stage, "Real next-stage scene transition failed.");
            manager = Object.FindFirstObjectByType<GameManager>();
            player = Object.FindFirstObjectByType<PlayerController2>();
            Require(manager != null && player != null && manager.CanPause, "Countdown or stage wiring failed.");
            float oldHp = player.hp, oldMaxHp = player.maxHp, oldSpeed = player.speed;
            PlayerController2 stagePlayer = player;
            runtimeRestores.Add(() => { if (stagePlayer != null) { stagePlayer.hp = oldHp; stagePlayer.maxHp = oldMaxHp; stagePlayer.speed = oldSpeed; } });
            player.hp = player.maxHp = 100f;
            player.speed = 5f;
            MovePlayer(new Vector3(0, 0.05f, -12));

            TreasureChest[] chests = Object.FindObjectsByType<TreasureChest>(FindObjectsSortMode.None);
            TreasureChest random = chests.Length == 1 ? chests[0] : null;
            Require(random != null, "Expected exactly one chest.");
            Require(random.SelectedIndex >= 0 && random.SelectedIndex < random.candidatePoints.Length &&
                random.candidatePoints[random.SelectedIndex] != null &&
                Vector3.Distance(random.transform.position, random.candidatePoints[random.SelectedIndex].position) < 0.05f,
                "Chest did not select a valid candidate.");
            // Isolate key/puzzle tests from the existing modal skill selection, which pauses time.
            foreach (Collider collider in random.GetComponentsInChildren<Collider>())
            {
                bool enabled = collider.enabled;
                runtimeRestores.Add(() => { if (collider != null) collider.enabled = enabled; });
                collider.enabled = false;
            }

            Enemy[] mobs = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)
                .Where(m => m.GetComponent<Rigidbody>() != null).ToArray();
            int[] expectedCounts = { 3, 3, 2, 4, 5 };
            Require(mobs.Length == expectedCounts[stage - 6], "Unexpected enemy count.");
            foreach (Enemy mob in mobs)
            {
                Require(FortStageValidator.HasBaselineSettings(mob), "Monster does not use the existing Enemy settings.");
                Require(FortStageValidator.HasBaselineRareSettings(mob), "Monster does not use the existing rare enemy settings.");
                Require(mob.GetComponents<MonoBehaviour>().All(component => component == null || component.GetType().Name != "FortEnemyBehaviour"),
                    "A monster still uses replacement enemy AI.");
                // Rare rewards open the existing modal skill picker; isolate it from these checks.
                RareEnemy rare = mob.GetComponent<RareEnemy>();
                if (rare != null)
                {
                    bool wasRare = ReadField<bool>(rare, "isRare");
                    runtimeRestores.Add(() => { if (rare != null) WriteField(rare, "isRare", wasRare); });
                    WriteField(rare, "isRare", false);
                }
                bool mobEnabled = mob.enabled;
                Rigidbody body = mob.GetComponent<Rigidbody>();
                RigidbodyConstraints constraints = body.constraints;
                runtimeRestores.Add(() => { if (mob != null) mob.enabled = mobEnabled; if (body != null) body.constraints = constraints; });
                mob.enabled = false;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.constraints = RigidbodyConstraints.FreezeAll;
            }
            foreach (FortTrap trap in Object.FindObjectsByType<FortTrap>(FindObjectsSortMode.None))
            {
                bool enabled = trap.enabled;
                runtimeRestores.Add(() => { if (trap != null) trap.enabled = enabled; });
                trap.enabled = false;
            }
            Require(ReadField<int>(manager, "collectedItems") == 0, "Fresh scene already has collected keys.");
            manager.TryGoal();
            Require(!ReadField<bool>(manager, "isGameOver") && manager.CanPause, "Goal accepted fewer than three keys.");
            Results.Add("Stage" + stage + ": one randomized chest; existing Enemy settings on all monsters; early goal rejected.");

            if (stage == 6) QueueEnemyChecks(mobs);
            if (stage == 7) QueueTrapChecks();
            if (stage == 8) QueueRuneChecks();
            if (stage == 9) QueueTowerChecks();
            QueueCarriers();
            for (int i = 0; i < 3; i++) QueueKey(i);
            Queue(0.1, "Stage" + stage + " exit approach", () =>
            {
                Require(manager.CollectedKeys == 3, "Three physical key pickups did not reach the shared counter.");
                // Reach the wall opening on foot and test the existing Door collision callback.
                GameManager stageObjective = manager;
                bool wasEnabled = stageObjective.enabled;
                runtimeRestores.Add(() => { if (stageObjective != null) stageObjective.enabled = wasEnabled; });
                stageObjective.enabled = false;
                MovePlayer(manager.goal.transform.position + new Vector3(0, 0.05f, -4f));
                player.GetComponent<StarterAssetsInputs>().move = Vector2.up;
            });
            Queue(1.2, "Stage" + stage + " clear and NextButton", () =>
            {
                player.GetComponent<StarterAssetsInputs>().move = Vector2.zero;
                Require(player.transform.position.z > manager.goal.transform.position.z - 1.5f && player.transform.position.y > -1f,
                    "Walking to the perimeter door did not safely reach its threshold.");
                Require(ReadField<bool>(manager, "isGameOver") && Time.timeScale == 0f && PlayerPrefs.GetInt("NextStage") == stage + 1,
                    "Goal did not clear the stage and save the next stage.");
                Results.Add("Stage" + stage + ": three keys collected through triggers; walked to perimeter door; collision clear and NextButton passed.");
                manager.OnNextButton();
                if (stage < 10)
                {
                    double countdownDeadline = EditorApplication.timeSinceStartup + 15;
                    Queue(4.4, "Stage" + (stage + 1) + " countdown", () => WaitForStageStart(stage + 1, countdownDeadline));
                }
                else QueueStage11Checks();
            });
        }

        private static void QueueEnemyChecks(Enemy[] mobs)
        {
            // The new cactus model must use the same actual Enemy physics and callbacks as Stage1.
            Enemy mob = mobs.First(m => m.GetComponentInChildren<Animator>().runtimeAnimatorController.name.Contains("Cactus"));
            // Isolate the shared melee behavior; ranged behavior has its own combat checks.
            bool usedRangedAttack = mob.usesRangedAttack;
            mob.usesRangedAttack = false;
            runtimeRestores.Add(() => { if (mob != null) mob.usesRangedAttack = usedRangedAttack; });
            Rigidbody body = mob.GetComponent<Rigidbody>();
            Rigidbody playerBody = player.GetComponent<Rigidbody>();
            Vector3 oldPosition = body.position;
            Quaternion oldRotation = body.rotation;
            float oldHp = mob.hp;
            float oldPlayerHp = player.hp;
            float oldAttackTime = ReadField<float>(mob, "lastAttackTime");
            RigidbodyConstraints oldPlayerConstraints = playerBody.constraints;
            Vector3 beganPursuit = Vector3.zero;
            float contactHp = 0;
            runtimeRestores.Add(() =>
            {
                if (mob != null) { mob.hp = oldHp; WriteField(mob, "lastAttackTime", oldAttackTime); }
                if (body != null) { body.position = oldPosition; body.rotation = oldRotation; }
                if (playerBody != null) playerBody.constraints = oldPlayerConstraints;
            });
            Queue(0.05, "Stage6 new monster pursues with Enemy", () =>
            {
                MovePlayer(new Vector3(0, 0.05f, -10));
                body.position = new Vector3(0, 0.05f, -14);
                mob.transform.position = body.position;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
                mob.enabled = true;
                beganPursuit = body.position;
                Physics.SyncTransforms();
            });
            Queue(0.25, "Stage6 Enemy damage and knockback", () =>
            {
                Require(body.position.z > beganPursuit.z + 0.3f, "New monster did not pursue using Enemy.FixedUpdate.");
                Require(!mob.TakeDamage(0.25f) && Mathf.Approximately(mob.hp, oldHp - 0.25f), "Enemy partial damage failed.");
                mob.Knockback(Vector3.right);
                Require(ReadField<bool>(mob, "isKnockedBack") && Mathf.Approximately(body.linearVelocity.x, mob.knockbackForce),
                    "Enemy knockback did not apply its baseline velocity.");
            });
            Queue(0.2, "Stage6 Enemy knockback recovery and contact", () =>
            {
                Require(!ReadField<bool>(mob, "isKnockedBack"), "Enemy knockback did not recover after its duration.");
                mob.hp = oldHp;
                WriteField(mob, "lastAttackTime", -Mathf.Infinity);
                body.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                playerBody.constraints = RigidbodyConstraints.FreezeRotation | RigidbodyConstraints.FreezePositionX | RigidbodyConstraints.FreezePositionZ;
                MovePlayer(new Vector3(0, 0.05f, -12));
                body.position = new Vector3(0, 0.05f, -11.3f);
                mob.transform.position = body.position;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                contactHp = player.hp;
                Physics.SyncTransforms();
            });
            Queue(0.2, "Stage6 physical Enemy contact damage", () =>
                Require(Mathf.Approximately(player.hp, contactHp - mob.attackDamage), "Physical Enemy contact did not deal exactly one hit."));
            Queue(0.2, "Stage6 Enemy contact cooldown", () =>
                Require(Mathf.Approximately(player.hp, contactHp - mob.attackDamage), "Enemy contact repeated before its one-second interval."));
            Queue(0.9, "Stage6 Enemy contact interval and restore", () =>
            {
                Require(Mathf.Approximately(player.hp, contactHp - 2f * mob.attackDamage), "Enemy contact did not repeat at its one-second interval.");
                mob.enabled = false;
                body.constraints = RigidbodyConstraints.FreezeAll;
                body.linearVelocity = body.angularVelocity = Vector3.zero;
                body.position = oldPosition;
                body.rotation = oldRotation;
                mob.transform.SetPositionAndRotation(oldPosition, oldRotation);
                playerBody.constraints = oldPlayerConstraints;
                player.hp = oldPlayerHp;
                MovePlayer(new Vector3(0, 0.05f, -12));
                Results.Add("Stage6: new cactus pursues, takes damage, knocks back and recovers; physical contact damage obeys the one-second interval.");
            });
        }

        private static void QueueCarriers()
        {
            Enemy[] carriers = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None).Where(e => e.keyObject != null).ToArray();
            GameObject[] drops = carriers.Select(c => c.keyObject).ToArray();
            Require(carriers.Length == (currentStage == 10 ? 3 : 1), "Unexpected key carrier count.");
            Queue(0.1, "Stage" + currentStage + " defeat key carriers", () =>
            {
                foreach (Enemy carrier in carriers)
                {
                    Require(carrier.keyObject != null && !carrier.keyObject.activeSelf, "Carrier/key wiring failed.");
                    Require(carrier.TakeDamage(1000f), "Carrier did not report defeat.");
                }
            });
            Queue(0.2, "Stage" + currentStage + " active key drops", () =>
                Require(drops.All(key => key != null && key.activeInHierarchy), "A defeated carrier did not release its existing key."));
        }

        private static void QueueKey(int index)
        {
            GameObject key = null;
            int before = 0;
            bool attemptedPickup = false;
            Queue(0.05, "Stage" + currentStage + " physical key " + (index + 1), () =>
            {
                key = manager.keys[index];
                before = ReadField<int>(manager, "collectedItems");
                if (key == null) return; // Stage9's walking check has already collected the reward.
                Require(key.activeInHierarchy, "Key " + (index + 1) + " is still locked.");
                attemptedPickup = true;
                MovePlayer(key.transform.position);
            });
            Queue(0.25, "Stage" + currentStage + " key trigger " + (index + 1), () =>
            {
                Require(manager.keys[index] == null, "Actual key trigger did not destroy the pickup.");
                int collected = ReadField<int>(manager, "collectedItems");
                Require(collected == before + (attemptedPickup ? 1 : 0), "Key trigger count was not exactly one.");
            });
        }

        private static void QueueTrapChecks()
        {
            FortTrap trap = Object.FindObjectsByType<FortTrap>(FindObjectsSortMode.None).OrderBy(t => Mathf.Abs(t.phaseOffset)).First();
            float hp = 0, pausedElapsed = 0;
            Queue(0.05, "Stage7 safe trap", () =>
            {
                hp = player.hp;
                WriteField(trap, "elapsed", 0.05f - trap.phaseOffset);
                trap.enabled = true;
                MovePlayer(trap.transform.position + Vector3.up * 0.05f);
            });
            Queue(0.25, "Stage7 warning trap", () =>
            {
                Require(Mathf.Approximately(player.hp, hp), "Safe trap damaged the player.");
                WriteField(trap, "elapsed", trap.safeTime + 0.05f - trap.phaseOffset);
            });
            Queue(0.25, "Stage7 active trap", () =>
            {
                Require(Mathf.Approximately(player.hp, hp), "Warning trap damaged the player.");
                WriteField(trap, "elapsed", trap.safeTime + trap.warningTime + 0.05f - trap.phaseOffset);
            });
            Queue(0.18, "Stage7 single trap hit", () =>
                Require(Mathf.Approximately(player.hp, hp - trap.damage), "Active trap did not deal exactly one hit."));
            Queue(0.18, "Stage7 no repeated hit and Pause", () =>
            {
                Require(Mathf.Approximately(player.hp, hp - trap.damage), "Trap repeated damage during one activation.");
                manager.PauseGame();
                Require(manager.IsPaused && Time.timeScale == 0f, "Pause did not stop gameplay.");
                pausedElapsed = ReadField<float>(trap, "elapsed");
            });
            Queue(0.2, "Stage7 frozen elapsed and Resume", () =>
            {
                Require(Mathf.Approximately(ReadField<float>(trap, "elapsed"), pausedElapsed) && Mathf.Approximately(player.hp, hp - trap.damage),
                    "Paused trap advanced or damaged the player.");
                manager.ResumeGame();
            });
            Queue(0.15, "Stage7 resumed elapsed", () =>
            {
                Require(ReadField<float>(trap, "elapsed") > pausedElapsed + 0.05f && Mathf.Approximately(player.hp, hp - trap.damage), "Trap did not resume without a repeated hit.");
                trap.enabled = false;
                MovePlayer(new Vector3(0, 0.05f, -12));
                Results.Add("Stage7: safe/warning cause no damage; one active hit; pause freezes and resume advances trap.");
            });
        }

        private static void QueueRuneChecks()
        {
            FortPuzzle puzzle = manager.puzzle;
            Require(puzzle != null && puzzle.mode == FortPuzzle.Mode.OrderedRunes, "Ordered rune puzzle missing.");
            Queue(0.05, "Stage8 wrong rune retry", () =>
            {
                puzzle.Activate(1);
                Require(puzzle.Progress == 0 && puzzle.LastInputWrong && !puzzle.Solved && !puzzle.rewardKey.activeSelf && puzzle.gate.activeSelf,
                    "Wrong rune did not reset while preserving the locked reward.");
            });
            QueueSwitch(puzzle, 0, 1);
            QueueSwitch(puzzle, 2, 2);
            QueueSwitch(puzzle, 1, 3);
            Queue(0.05, "Stage8 unlocked reward", () =>
            {
                Require(puzzle.Solved && !puzzle.LastInputWrong && puzzle.rewardKey.activeInHierarchy && !puzzle.gate.activeSelf,
                    "Correct physical rune sequence did not release the key/gate.");
                Results.Add("Stage8: wrong input resets; physical rune entries 0 -> 2 -> 1 solve and unlock.");
            });
        }

        private static void QueueSwitch(FortPuzzle puzzle, int index, int expectedProgress)
        {
            FortPuzzleSwitch action = Object.FindObjectsByType<FortPuzzleSwitch>(FindObjectsSortMode.None).First(s => s.puzzle == puzzle && s.index == index);
            Queue(0.05, "Stage" + currentStage + " enter switch " + index, () => MovePlayer(new Vector3(action.transform.position.x, 0.05f, action.transform.position.z)));
            Queue(0.3, "Stage" + currentStage + " switch " + index + " progress", () => Require(puzzle.Progress == expectedProgress, "Physical switch entry produced the wrong progress."));
        }

        private static void QueueTowerChecks()
        {
            FortPuzzle puzzle = manager.puzzle;
            Require(puzzle != null && puzzle.mode == FortPuzzle.Mode.TwoSwitches, "Two-switch puzzle missing.");
            Queue(0.05, "Stage9 new monster Enemy settings", () =>
            {
                Enemy[] mobs = Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)
                    .Where(m => m.GetComponent<Rigidbody>() != null).ToArray();
                Enemy mushroom = mobs.First(m => m.GetComponentInChildren<Animator>().runtimeAnimatorController.name.Contains("Mushroom"));
                Enemy cactus = mobs.First(m => m.GetComponentInChildren<Animator>().runtimeAnimatorController.name.Contains("Cactus"));
                Require(Mathf.Approximately(mushroom.speed, 3f) && Mathf.Approximately(cactus.speed, 3f) &&
                    Mathf.Approximately(mushroom.attackDamage, 1f) && Mathf.Approximately(cactus.attackDamage, 1f) &&
                    Mathf.Approximately(mushroom.attackInterval, 1f) && Mathf.Approximately(cactus.attackInterval, 1f),
                    "New monster visuals do not share the baseline contact attack settings.");
            });
            QueueSwitch(puzzle, 0, 1);
            FortPuzzleSwitch first = Object.FindObjectsByType<FortPuzzleSwitch>(FindObjectsSortMode.None).First(s => s.puzzle == puzzle && s.index == 0);
            Queue(0.05, "Stage9 leave first switch", () => MovePlayer(first.transform.position + new Vector3(3, 0, 0)));
            Queue(0.3, "Stage9 first switch exit", () => Require(puzzle.Progress == 1, "Leaving a switch changed progress."));
            QueueSwitch(puzzle, 0, 1);
            QueueSwitch(puzzle, 1, 2);
            GameObject reward = puzzle.rewardKey;
            Queue(0.05, "Stage9 walk into unlocked reward chamber", () =>
            {
                Require(puzzle.Solved && reward.activeInHierarchy && !puzzle.gate.activeSelf, "Two distinct seals did not open the reward chamber.");
                MovePlayer(new Vector3(0, 0.05f, 18));
                player.GetComponent<StarterAssetsInputs>().move = Vector2.up;
            });
            Queue(1.1, "Stage9 continuous entrance and reward pickup", () =>
            {
                player.GetComponent<StarterAssetsInputs>().move = Vector2.zero;
                player.GetComponent<Rigidbody>().linearVelocity = Vector3.zero;
                Require(player.transform.position.z > 22 && player.transform.position.y > -1 && reward == null,
                    "Walking through the opened entrance did not safely reach and collect the reward.");
                Results.Add("Stage9: new monsters share Enemy contact attack settings; revisiting one seal stays 1/2; second seal opens a walkable entrance and reward trigger.");
            });
        }

        private static void QueueStage11Checks()
        {
            Queue(0.5, "Stage10 -> Stage11", () =>
            {
                Require(SceneManager.GetActiveScene().name == "Stage11", "Stage10 NextButton did not load Stage11.");
                GameManager nextManager = Object.FindFirstObjectByType<GameManager>();
                Require(nextManager != null && !nextManager.CanPause, "Expected Stage11 opening countdown.");
                nextManager.LoadLobbyScene();
            });
            Queue(0.2, "Lobby excludes stage shortcuts", () =>
            {
                Require(SceneManager.GetActiveScene().name == "LobbyScene", "Return to lobby failed.");
                Require(GameObject.Find("FortStageEntry") == null, "Removed stage-selection menu was recreated.");
                Require(Object.FindFirstObjectByType<SceneLoader>() != null, "The lobby's original Play handler is missing.");
                Results.Add("Stage10 -> Stage11 and return to lobby passed; stage-selection shortcuts are absent.");
                Finish(true, "All five stages and Stage11 transition passed.");
            });
        }

        private static void MovePlayer(Vector3 position)
        {
            Require(player != null, "Player missing.");
            StarterAssetsInputs input = player.GetComponent<StarterAssetsInputs>();
            Require(input != null, "Player movement input missing.");
            input.move = Vector2.zero;
            Rigidbody body = player.GetComponent<Rigidbody>();
            Require(body != null, "Player Rigidbody missing.");
            body.linearVelocity = body.angularVelocity = Vector3.zero;
            body.position = position;
            player.transform.position = position;
            Physics.SyncTransforms();
        }

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }

        private static T ReadField<T>(object owner, string name)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "Private field missing: " + name);
            return (T)field.GetValue(owner);
        }

        private static void WriteField<T>(object owner, string name, T value)
        {
            FieldInfo field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Require(field != null, "Private field missing: " + name);
            field.SetValue(owner, value);
        }

        private static void SnapshotPreferences()
        {
            savedPrefs.Clear();
            foreach (string key in new[] { "NextStage", "Carry_HasData" }) Snapshot(key, PrefType.Int);
            foreach (string key in new[] { "Carry_Hp", "Carry_MaxHp", "Carry_Speed", "Carry_Attack", "Carry_Reload", "Carry_CameraY" }) Snapshot(key, PrefType.Float);
            Snapshot("Carry_Skills", PrefType.String);
            for (int stage = 6; stage <= 10; stage++) Snapshot("BestTime_Stage" + stage, PrefType.Float);
        }

        private static void Snapshot(string key, PrefType type)
        {
            savedPrefs.Add(new SavedPref { Key = key, Type = type, Existed = PlayerPrefs.HasKey(key),
                IntValue = type == PrefType.Int ? PlayerPrefs.GetInt(key) : 0,
                FloatValue = type == PrefType.Float ? PlayerPrefs.GetFloat(key) : 0f,
                StringValue = type == PrefType.String ? PlayerPrefs.GetString(key) : null });
        }

        private static void OnPlayModeChanged(PlayModeStateChange state)
        {
            if (state == PlayModeStateChange.ExitingPlayMode && running) Finish(false, "Play mode stopped before completion.");
        }

        private static void OnAssemblyReload()
        {
            if (running) Finish(false, "Assembly reload interrupted the checks.");
        }

        private static void Finish(bool passed, string reason)
        {
            if (!running) return;
            running = false;
            steps.Clear();
            EditorApplication.update -= Tick;
            EditorApplication.playModeStateChanged -= OnPlayModeChanged;
            AssemblyReloadEvents.beforeAssemblyReload -= OnAssemblyReload;
            var cleanupErrors = new List<string>();
            try
            {
                if (player != null) { player.GetComponent<StarterAssetsInputs>().move = Vector2.zero; }
                if (manager != null && manager.IsPaused) manager.ResumeGame();
                for (int i = runtimeRestores.Count - 1; i >= 0; i--) runtimeRestores[i]();
            }
            catch (Exception exception) { cleanupErrors.Add(exception.Message); }
            finally
            {
                foreach (SavedPref pref in savedPrefs)
                {
                    try
                    {
                        if (!pref.Existed) PlayerPrefs.DeleteKey(pref.Key);
                        else if (pref.Type == PrefType.Int) PlayerPrefs.SetInt(pref.Key, pref.IntValue);
                        else if (pref.Type == PrefType.Float) PlayerPrefs.SetFloat(pref.Key, pref.FloatValue);
                        else PlayerPrefs.SetString(pref.Key, pref.StringValue);
                    }
                    catch (Exception exception) { cleanupErrors.Add(pref.Key + ": " + exception.Message); }
                }
                try { PlayerPrefs.Save(); }
                catch (Exception exception) { cleanupErrors.Add("PlayerPrefs.Save: " + exception.Message); }
                try { Application.runInBackground = oldRunInBackground; }
                catch (Exception exception) { cleanupErrors.Add("runInBackground: " + exception.Message); }
                runtimeRestores.Clear();
                savedPrefs.Clear();
            }
            if (cleanupErrors.Count > 0) { passed = false; reason += " Cleanup: " + string.Join("; ", cleanupErrors); }
            Status = (passed ? "Passed: " : "Failed: ") + reason;
            Results.Add(Status + " Preferences and runInBackground restored; elapsed " + (EditorApplication.timeSinceStartup - began).ToString("F1") + "s.");
            Debug.Log(Report);
        }
    }
}
