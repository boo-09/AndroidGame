using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FortStages.Editor
{
    /// <summary>Creates independent copies of Stage1. Shared assets are only read.</summary>
    public static class FortStageBuilder
    {
        public const string SceneFolder = "Assets/Scenes";
        private const string Fort = "Assets/Design/Layer Lab/3D Props-CartoonBoxMap/Prefabs/06_Fort/";
        private const string Monsters = "Assets/Design/Characters/Monsters/";
        private static readonly string[] Visuals =
        {
            Monsters + "RPG Monster DUO PBR Polyart/Prefabs/PolyartDefault/SlimePolyart.prefab",
            Monsters + "RPG Monster DUO PBR Polyart/Prefabs/PolyartDefault/TurtleShellPolyart.prefab",
            Monsters + "RPGMonsterBuddiesPBRPA/Prefab/CactusPA.prefab",
            Monsters + "RPGMonsterBuddiesPBRPA/Prefab/MushroomSmilePA.prefab",
            Monsters + "RPGMonsterPartnersPBRPolyart/Prefabs/Character/BeholderPolyartDefault.prefab"
        };
        private static readonly string[] Titles =
        { "06  |  門番の中庭", "07  |  灼熱の回廊", "08  |  三紋の封印庭園",
          "09  |  支援兵の双塔", "10  |  最後の砦" };

        [MenuItem("Tools/Fort Stages/Create Missing Stages 6-10")]
        public static void CreateFromMenu() { Debug.Log(BuildAll()); }

        public static string BuildAll()
        {
            return BuildStages(6, 10, false);
        }

        [MenuItem("Tools/Fort Stages/Create Stages 3-5 (Rebuild)")]
        public static void CreateStages345FromMenu()
        {
            if (!EditorUtility.DisplayDialog("Stage3-5 を作成",
                "既存の Stage3 / Stage4 / Stage5 を削除して作り直します。よろしいですか？",
                "作成する", "やめる")) return;
            Debug.Log(BuildStages(3, 5, true));
        }

        private static string BuildStages(int firstStage, int lastStage, bool rebuild)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for (int i = 0; i < SceneManager.sceneCount; i++)
                if (SceneManager.GetSceneAt(i).isDirty) throw new InvalidOperationException("Save your current scene before creating stages.");
            EnsureFolder(SceneFolder);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Materials");
            PrepareFont();
            var setup = EditorSceneManager.GetSceneManagerSetup();
            var created = new List<int>();
            try
            {
                for (int stage = firstStage; stage <= lastStage; stage++)
                {
                    string path = SceneFolder + "/Stage" + stage + ".unity";
                    if (File.Exists(path))
                    {
                        if (!rebuild) continue;
                        AssetDatabase.DeleteAsset(path);
                    }
                    if (rebuild)
                    {
                        string layout = "Assets/Prefabs/FortLayout" + stage + ".prefab";
                        if (File.Exists(layout)) AssetDatabase.DeleteAsset(layout);
                    }
                    if (!AssetDatabase.CopyAsset(SceneFolder + "/Stage1.unity", path))
                        throw new InvalidOperationException("Stage1 copy failed: " + path);
                    var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    if (stage <= 5) BuildEarlyStageCopy(stage, scene);
                    else BuildCopy(stage, scene);
                    created.Add(stage);
                }
                RegisterBuildScenes();
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return "Created stages: " + string.Join(", ", created);
        }

        [MenuItem("Tools/Fort Stages/Register Scenes In Build")]
        public static void RegisterBuildScenes()
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            for (int stage = 3; stage <= 10; stage++)
            {
                string path = SceneFolder + "/Stage" + stage + ".unity";
                if (!File.Exists(path)) continue;
                int index = scenes.FindIndex(s => s.path == path);
                if (index < 0) scenes.Add(new EditorBuildSettingsScene(path, true));
                else scenes[index] = new EditorBuildSettingsScene(path, true);
            }
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        [MenuItem("Tools/Fort Stages/Open Stage 6")]
        public static void OpenStage6()
        {
            if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                EditorSceneManager.OpenScene(SceneFolder + "/Stage6.unity");
        }

        private static void BuildCopy(int stage, Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var rooms = roots.First(g => g.name == "Rooms");
            var enemies = roots.First(g => g.name == "Enemies");
            var keysRoot = roots.First(g => g.name == "Keys");
            ClearChildren(rooms.transform);
            ClearChildren(enemies.transform);
            ClearChildren(keysRoot.transform);
            foreach (var e in enemies.GetComponents<Enemy>()) Object.DestroyImmediate(e);
            var extension = new GameObject("FortStage" + stage);
            var player = roots.First(g => g.name == "Player").GetComponent<PlayerController2>();
            player.transform.SetPositionAndRotation(new Vector3(0, 0.05f, -12), Quaternion.identity);
            var gm = roots.First(g => g.name == "GameManager").GetComponent<GameManager>();
            var camera = roots.First(g => g.name == "Main Camera");
            camera.transform.position = new Vector3(0, 13, -20);
            var cameraSettings = new SerializedObject(camera.GetComponent<CameraController>());
            cameraSettings.FindProperty("fixedY").floatValue = 13;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();
            // Stage1 relies on ambient illumination. New fort stages also have a main light.
            var sun = new GameObject("Fort Sun", typeof(Light));
            sun.transform.SetParent(extension.transform);
            sun.transform.rotation = Quaternion.Euler(52, -28, 0);
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.color = stage == 7 ? new Color(1, 0.8f, 0.62f) : new Color(1, 0.95f, 0.83f);
            light.shadows = LightShadows.Soft;
            // Keep this light out of the procedural skybox's automatic sun selection.
            light.renderMode = LightRenderMode.ForceVertex;
            RenderSettings.ambientLight = new Color(0.65f, 0.71f, 0.75f);
            RenderSettings.fog = false;
            var palette = new[] { new Color(0.18f,0.55f,0.63f), new Color(0.78f,0.31f,0.13f),
                new Color(0.4f,0.48f,0.72f),new Color(0.32f,0.57f,0.4f),new Color(0.6f,0.38f,0.23f) };
            Material accent = MaterialAsset("Accent" + stage, palette[stage - 6]);
            Material gold = MaterialAsset("Gold", new Color(1, 0.73f, 0.16f));
            Material dark = MaterialAsset("Wall", new Color(0.24f, 0.3f, 0.32f));
            BuildTerrain(stage, rooms.transform, accent, dark);

            var chest = roots.First(g => g.name == "TreasureChest");
            var chestComponents = chest.GetComponents<TreasureChest>();
            for (int i = 1; i < chestComponents.Length; i++) Object.DestroyImmediate(chestComponents[i]);
            chest.transform.position = new Vector3(-12, 0.1f, 9);
            var randomChest = chestComponents[0];
            Vector3[] chestPositions;
            switch (stage)
            {
                case 7: chestPositions = new[] { P(-9,0), P(9,12), P(-9,24), P(3,27) }; break;
                case 9: chestPositions = new[] { P(-12,6), P(12,6), P(-9,24), P(9,24) }; break;
                default: chestPositions = new[] { P(-12,9), P(12,9), P(-9,24), P(9,24) }; break;
            }
            var candidates = Child(extension.transform, "ChestCandidates");
            randomChest.candidatePoints = chestPositions.Select((p,i) =>
            {
                var point = Child(candidates, "Candidate" + (i + 1));
                point.position = p + Vector3.up * 0.1f;
                return point;
            }).ToArray();

            var goal = roots.First(g => g.GetComponent<Door>() != null).GetComponent<Door>();
            goal.transform.position = new Vector3(0, 0, 33);
            goal.transform.rotation = Quaternion.Euler(0, 90, 0);
            goal.gameObject.layer = LayerMask.NameToLayer("Obstacle");
            var goalCollider = goal.GetComponent<BoxCollider>();
            if (goalCollider == null) goalCollider = goal.gameObject.AddComponent<BoxCollider>();
            goalCollider.center = new Vector3(0, 1, 0);
            goalCollider.size = new Vector3(1, 3, 3 / Mathf.Abs(goal.transform.lossyScale.z));
            goalCollider.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(goalCollider);
            GroundLabel(extension.transform, "NORTH GATE\n3 KEYS REQUIRED", P(0,30.8f), gold, 2.7f);

            var keys = new[] { NewKey(keysRoot.transform, 1, P(-9,6)),
                NewKey(keysRoot.transform, 2, P(9,15)), NewKey(keysRoot.transform, 3, P(0,23)) };
            var mobs = new List<Enemy>();
            switch (stage)
            {
                case 6:
                    mobs.Add(Mob(enemies.transform, 0, P(-7,3)));
                    mobs.Add(Mob(enemies.transform, 2, P(10,12)));
                    mobs.Add(Mob(enemies.transform, 1, P(0,20)));
                    Carrier(extension.transform, mobs[2], keys[2], gold);
                    break;
                case 7:
                    keys[0].transform.position = P(-9,0) + Vector3.up * 0.6f;
                    keys[1].transform.position = P(9,12) + Vector3.up * 0.6f;
                    mobs.Add(Mob(enemies.transform, 0, P(1,0)));
                    mobs.Add(Mob(enemies.transform, 2, P(-9,24)));
                    mobs.Add(Mob(enemies.transform, 1, P(0,18)));
                    Carrier(extension.transform, mobs[1], keys[2], gold);
                    foreach (int z in new[] { -3, 6, 18 }) Trap(extension.transform, P(0,z), stage, (z+3)/3f, accent);
                    break;
                case 8:
                    mobs.Add(Mob(enemies.transform, 0, P(-9,3)));
                    mobs.Add(Mob(enemies.transform, 4, P(11,12)));
                    Carrier(extension.transform, mobs[1], keys[1], gold);
                    Puzzle(extension.transform, FortPuzzle.Mode.OrderedRunes, keys[2],
                        new[] { P(-9,0), P(9,6), P(0,15) }, new[] { 0, 2, 1 }, dark);
                    GroundLabel(extension.transform, "CLUE: RED > GREEN > BLUE", P(0,-6), gold, 4);
                    break;
                case 9:
                    keys[0].transform.position = P(-9,12) + Vector3.up * 0.6f;
                    mobs.Add(Mob(enemies.transform, 0, P(-9,6)));
                    mobs.Add(Mob(enemies.transform, 1, P(9,9)));
                    mobs.Add(Mob(enemies.transform, 2, P(9,18)));
                    mobs.Add(Mob(enemies.transform, 3, P(12,15)));
                    Carrier(extension.transform, mobs[2], keys[1], gold);
                    Puzzle(extension.transform, FortPuzzle.Mode.TwoSwitches, keys[2],
                        new[] { P(-9,21), P(9,24) }, new[] { 0, 1 }, dark);
                    GroundLabel(extension.transform, "CLUE: LIGHT BOTH TOWER SEALS", P(0,-6), gold, 4);
                    break;
                default:
                    mobs.Add(Mob(enemies.transform, 1, P(-9,6)));
                    mobs.Add(Mob(enemies.transform, 4, P(9,15)));
                    mobs.Add(Mob(enemies.transform, 1, P(0,23)));
                    mobs.Add(Mob(enemies.transform, 3, P(9,21)));
                    mobs.Add(Mob(enemies.transform, 0, P(-6,18)));
                    for (int i=0;i<3;i++) Carrier(extension.transform, mobs[i], keys[i], gold);
                    Trap(extension.transform, P(0,3), stage, 0, accent);
                    Trap(extension.transform, P(6,12), stage, 1.5f, accent);
                    break;
            }
            var objective = gm;
            objective.stageTitle = Titles[stage-6];
            objective.keys = keys;
            objective.goal = goal;
            objective.puzzle = extension.GetComponentInChildren<FortPuzzle>();
            GroundLabel(extension.transform, "EXPLORE  |  FIGHT  |  COLLECT 3 KEYS", P(0,-9), accent, 4);
            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene)) throw new InvalidOperationException("Could not save " + scene.path);
        }

        private static Vector3 P(float x, float z) { return new Vector3(x,0,z); }
        private static void BuildTerrain(int stage, Transform parent, Material accent, Material dark)
        {
            var grounds = Child(parent, "Grounds");
            var boundaries = Child(parent, "Boundaries_CaveField02_Only");
            var landmarks = Child(parent, "FortLandmarks");
            var cells = new HashSet<Vector2Int>();
            for (int x=-5;x<=5;x++)
                for (int z=-5;z<=10;z++)
                {
                    bool floor = true;
                    if (stage == 7) floor = Math.Abs(x)<=1 ||
                        (x<=0 && (z>=-1&&z<=1 || z>=7&&z<=9)) || (x>=0 && z>=3&&z<=5);
                    if (stage == 9) floor = Math.Abs(x)>=2 || z<=-2 || z>=6 || (z>=2&&z<=4);
                    if (stage == 10) floor = !(Math.Abs(x)<=1 && (z==0 || z==1)) || x==0;
                    if (floor) cells.Add(new Vector2Int(x,z));
                }
            cells.Add(new Vector2Int(0,11)); // Floor threshold in the north perimeter doorway.
            foreach (var c in cells.OrderBy(c=>c.y).ThenBy(c=>c.x))
            {
                string tile = (c.x+c.y+stage)%4 == 0 ? "Fort02_4" : "Fort01_4";
                var floor = Instantiate(Fort+"Map/"+tile+".prefab", grounds, P(c.x*3,c.y*3), Vector3.one*3);
                floor.name = "FortFloor_"+c.x+"_"+c.y;
                BoxFromMesh(floor, false);
            }
            // Cave_Field_02 is confined to the outside rim; interior holes remain open fort courtyards.
            for(int x=-6;x<=6;x++)
                for(int z=-6;z<=11;z++)
                {
                    if(x!=-6&&x!=6&&z!=-6&&z!=11) continue;
                    if(x==0&&z==11) continue; // Keep the north gate opening clear.
                    var boundary = Instantiate("Assets/Prefabs/Cave_Field_02.prefab", boundaries,
                        P(x*3,z*3)+Vector3.up*1.8f, Vector3.one*3);
                    boundary.name="Boundary_CaveField02_"+x+"_"+z;
                    BoxFromMesh(boundary,true);
                }
            // Low fort columns/stone cover read as an outdoor fortress from the existing top-down camera.
            foreach(var p in new[]{P(-15,-12),P(15,-12),P(-15,27),P(15,27)})
            {
                var column=Instantiate(Fort+"Side/Fort_Column.prefab",landmarks,p,Vector3.one*1.8f);
                BoxFromMesh(column,true);
            }
            Vector3[] covers;
            switch(stage)
            {
                case 7: covers=new[]{P(-5,0),P(5,12),P(-5,24)};break;
                case 8: covers=new[]{P(-4,6),P(4,12),P(-4.5f,21),P(4.5f,21)};break;
                case 9: covers=new[]{P(-12,9),P(6,15),P(12,24)};break;
                case 10: covers=new[]{P(-6,0),P(9,9),P(-9,15),P(3,21)};break;
                default: covers=new[]{P(-6,6),P(6,15),P(-12,21),P(12,21)};break;
            }
            foreach(var p in covers)
            {
                var cover=Instantiate(Fort+"Side/Fort_Stone_12.prefab",landmarks,p,Vector3.one*2);
                BoxFromMesh(cover,true);
            }
            if(stage==6)
            {
                Wall(landmarks,P(-7.5f,15),new Vector3(1.1f,2.5f,9),dark);
                Wall(landmarks,P(7.5f,3),new Vector3(1.1f,2.5f,9),dark);
            }
            // Save the new geometry as a stage-specific prefab: each artist can edit one file independently.
            string prefab="Assets/Prefabs/FortLayout"+stage+".prefab";
            if(!File.Exists(prefab)) PrefabUtility.SaveAsPrefabAssetAndConnect(parent.gameObject,prefab,InteractionMode.AutomatedAction);
        }

        private static Enemy Mob(Transform parent, int visual, Vector3 position)
        {
            string path="Assets/Prefabs/"+new[]{"SlimeGuard","TurtleCharger","CactusArcher","MushroomSupport","BeholderArcher"}[visual]+".prefab";
            GameObject go;
            if(File.Exists(path)) go=Instantiate(path,parent,position,Vector3.one);
            else
            {
                go=new GameObject(Path.GetFileNameWithoutExtension(path));
                go.transform.SetParent(parent,false);
                go.transform.position=position;
                go.layer=LayerMask.NameToLayer("Enemy");
                var model=Instantiate(Visuals[visual],go.transform,position,Vector3.one);
                model.name="Model";
                foreach(var t in model.GetComponentsInChildren<Transform>(true)) t.gameObject.layer=go.layer;
                foreach(var col in model.GetComponentsInChildren<Collider>(true)) Object.DestroyImmediate(col);
                go.AddComponent<Rigidbody>();
                var enemy=go.AddComponent<Enemy>();
                ConfigureEnemy(enemy,visual);
                PrefabUtility.SaveAsPrefabAssetAndConnect(go,path,InteractionMode.AutomatedAction);
            }
            var behavior=go.GetComponent<Enemy>();
            if(behavior==null) behavior=go.AddComponent<Enemy>();
            ConfigureEnemy(behavior,visual);
            PrefabUtility.RecordPrefabInstancePropertyModifications(behavior);
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<Rigidbody>());
            PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<SphereCollider>());
            if(go.GetComponent<RareEnemy>()!=null)
                PrefabUtility.RecordPrefabInstancePropertyModifications(go.GetComponent<RareEnemy>());
            return behavior;
        }

        /// <summary>Matches the saved Stage1-3 enemies; new monster visuals use the slime settings.</summary>
        public static void ConfigureEnemy(Enemy enemy,int visual)
        {
            var go=enemy.gameObject;
            // Remove the independent combat driver before enabling the shared Enemy behavior.
            foreach(var legacy in go.GetComponents<MonoBehaviour>().Where(b=>b!=null&&b.GetType().FullName=="FortStages.FortEnemyBehaviour").ToArray())
                Object.DestroyImmediate(legacy);
            foreach(var nestedRare in go.GetComponentsInChildren<RareEnemy>(true).Where(r=>r.gameObject!=go).ToArray())
                Object.DestroyImmediate(nestedRare);
            foreach(var nestedEnemy in go.GetComponentsInChildren<Enemy>(true).Where(e=>e!=enemy).ToArray())
                Object.DestroyImmediate(nestedEnemy);
            foreach(var nestedBody in go.GetComponentsInChildren<Rigidbody>(true).Where(b=>b.gameObject!=go).ToArray())
                Object.DestroyImmediate(nestedBody);
            var sphere=go.GetComponent<SphereCollider>();
            if(sphere==null) sphere=go.AddComponent<SphereCollider>();
            foreach(var collider in go.GetComponents<Collider>().Where(c=>c!=sphere).ToArray())
                Object.DestroyImmediate(collider);
            sphere.enabled=true;sphere.isTrigger=false;sphere.center=new Vector3(0,0.5f,0);sphere.radius=0.5f;
            var body=go.GetComponent<Rigidbody>();
            if(body==null) body=go.AddComponent<Rigidbody>();
            body.mass=1f;body.useGravity=true;body.isKinematic=false;
            body.constraints=RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ;
            body.interpolation=RigidbodyInterpolation.None;
            body.collisionDetectionMode=CollisionDetectionMode.Discrete;
            enemy.enabled=true;
            enemy.player=null;
            enemy.speed=3f;
            enemy.detectionRange=10f;
            enemy.obstacleLayer=LayerMask.GetMask("Obstacle");
            enemy.rotateSpeed=5f;
            enemy.hp=1f;
            enemy.attackDamage=visual==1?2f:1f;
            enemy.knockbackForce=25f;
            enemy.knockbackDuration=0.1f;
            enemy.attackInterval=1f;
            enemy.fallDeathY=-10f;
            enemy.jumpForce=8f;
            enemy.chestCheckDistance=visual==1?1.5f:2f;
            enemy.chestCheckRadius=0.5f;
            enemy.jumpCooldown=visual==1?1f:0.8f;
            var rare=go.GetComponent<RareEnemy>();
            if(visual==1)
            {
                if(rare!=null) Object.DestroyImmediate(rare);
            }
            else
            {
                if(rare==null) rare=go.AddComponent<RareEnemy>();
                rare.enabled=true;
                var settings=new SerializedObject(rare);
                settings.FindProperty("rareChance").floatValue=0.1f;
                settings.FindProperty("rareColor").colorValue=new Color(1f,0.84f,0f);
                settings.FindProperty("rareScale").floatValue=1.2f;
                settings.FindProperty("hpMultiplier").floatValue=1f;
                settings.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static GameObject NewKey(Transform parent,int number,Vector3 position)
        {
            var key=Instantiate("Assets/Prefabs/97_Othert_Key_Yellow.prefab",parent,
                position+Vector3.up*0.6f,Vector3.one);
            key.name="FortKey"+number;
            // Avoid the old PickUp tag consuming a key before the manager receives it.
            key.tag="Untagged";
            var col=key.GetComponent<BoxCollider>();
            if(col==null) col=key.AddComponent<BoxCollider>();
            col.isTrigger=true;col.center=Vector3.zero;col.size=new Vector3(1.5f,1.5f,1.5f);
            if(key.GetComponent<Key>()==null) key.AddComponent<Key>();
            PrefabUtility.RecordPrefabInstancePropertyModifications(col);
            PrefabUtility.RecordPrefabInstancePropertyModifications(key);
            return key;
        }

        private static void Carrier(Transform parent,Enemy mob,GameObject key,Material gold)
        {
            key.SetActive(false);
            mob.keyObject = key;
            PrefabUtility.RecordPrefabInstancePropertyModifications(mob);
            var marker=GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name="Gold key carrier marker";marker.transform.SetParent(mob.transform,false);
            marker.transform.localPosition=new Vector3(0,2.2f,0);marker.transform.localScale=Vector3.one*0.45f;
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            marker.GetComponent<Renderer>().sharedMaterial=gold;
            GroundLabel(parent,"GOLD MARK = KEY CARRIER",mob.transform.position+Vector3.back*2,gold,3.2f);
        }

        private static void Trap(Transform parent,Vector3 position,int stage,float offset,Material accent)
        {
            var trap=Instantiate(Fort+"Map/Fort_Trap.prefab",parent,position+Vector3.up*0.035f,Vector3.one*3);
            trap.name="TelegraphedTrap_"+position.x+"_"+position.z;
            var trigger=trap.AddComponent<BoxCollider>();trigger.isTrigger=true;
            trigger.center=new Vector3(0,0.3f,0);trigger.size=new Vector3(0.95f,0.6f,0.95f);
            var warning=GameObject.CreatePrimitive(PrimitiveType.Cube);
            warning.name="Warning panel";warning.transform.SetParent(trap.transform,false);
            warning.transform.localPosition=new Vector3(0,stage <= 5 ? 0.03f : 0.025f,0);
            warning.transform.localScale=stage <= 5 ? new Vector3(0.95f,0.02f,0.95f) : new Vector3(0.91f,0.013f,0.91f);
            Object.DestroyImmediate(warning.GetComponent<Collider>());
            warning.GetComponent<Renderer>().sharedMaterial=MaterialAsset("TrapWarning",new Color(0.2f,0.35f,0.4f));
            var behavior=trap.AddComponent<FortTrap>();behavior.warningRenderer=warning.GetComponent<Renderer>();
            behavior.safeTime=3.2f;behavior.warningTime=1.4f;behavior.activeTime=0.8f;behavior.phaseOffset=offset;
            if (stage >= 6) GroundLabel(parent,"AMBER: WARNING\nRED: FIRE",position+Vector3.left*3,accent,2.5f);
        }

        private static void Puzzle(Transform parent,FortPuzzle.Mode mode,GameObject reward,
            Vector3[] switches,int[] sequence,Material wallMaterial)
        {
            var group=Child(parent,"FortPuzzle");
            var puzzle=group.gameObject.AddComponent<FortPuzzle>();
            puzzle.mode=mode;puzzle.sequence=sequence;puzzle.rewardKey=reward;
            reward.transform.position=P(0,23)+Vector3.up*0.6f;reward.SetActive(false);
            var gate=Wall(group,P(0,20),new Vector3(5.5f,2.4f,0.65f),wallMaterial);
            gate.name="RuneGate";puzzle.gate=gate;
            // Enclosure only surrounds the reward; every switch remains accessible before opening it.
            Wall(group,P(-3.1f,23),new Vector3(0.65f,2.4f,6.7f),wallMaterial);
            Wall(group,P(3.1f,23),new Vector3(0.65f,2.4f,6.7f),wallMaterial);
            Wall(group,P(0,26),new Vector3(6.7f,2.4f,0.65f),wallMaterial);
            var colors=new[]{new Color(0.9f,0.15f,0.13f),new Color(0.18f,0.45f,0.94f),new Color(0.22f,0.78f,0.32f)};
            var names=new[]{"RED","BLUE","GREEN"};
            var indicators=new List<Renderer>();
            for(int i=0;i<switches.Length;i++)
            {
                var seal=GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                seal.name=mode==FortPuzzle.Mode.OrderedRunes?names[i]+" rune":"Tower seal "+(i+1);
                seal.transform.SetParent(group,false);seal.transform.position=switches[i]+Vector3.up*0.06f;
                seal.transform.localScale=new Vector3(2.3f,0.06f,2.3f);
                Object.DestroyImmediate(seal.GetComponent<Collider>());
                var trigger=seal.AddComponent<BoxCollider>();trigger.isTrigger=true;
                trigger.center=new Vector3(0,5,0);trigger.size=new Vector3(1,12,1);
                var renderer=seal.GetComponent<Renderer>();renderer.sharedMaterial=MaterialAsset("Rune"+i,colors[i]);
                indicators.Add(renderer);
                var action=seal.AddComponent<FortPuzzleSwitch>();action.puzzle=puzzle;action.index=i;
                GroundLabel(group,mode==FortPuzzle.Mode.OrderedRunes?names[i]:"TOWER "+(i+1),
                    switches[i]+Vector3.back*1.7f,renderer.sharedMaterial,2.4f);
            }
            puzzle.indicators=indicators.ToArray();
        }

        private static GameObject Wall(Transform parent,Vector3 p,Vector3 size,Material material)
        {
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name="Fort barricade";wall.transform.SetParent(parent,false);
            wall.transform.position=p+Vector3.up*(size.y/2);wall.transform.localScale=size;
            wall.gameObject.layer=LayerMask.NameToLayer("Obstacle");wall.GetComponent<Renderer>().sharedMaterial=material;
            return wall;
        }

        private static void GroundLabel(Transform parent,string caption,Vector3 position,Material material,float width)
        {
            var go=new GameObject(caption.Replace('\n',' '),typeof(TextMeshPro));go.transform.SetParent(parent,false);
            go.transform.position=position+Vector3.up*0.09f;go.transform.rotation=Quaternion.Euler(90,0,0);
            var text=go.GetComponent<TextMeshPro>();text.text=caption;text.fontSize=2.2f;
            text.rectTransform.sizeDelta=new Vector2(width*2.5f,2.5f);text.alignment=TextAlignmentOptions.Center;
            text.color=material.color;text.raycastTarget=false;
            text.font=FortFont();
        }

        private static Material MaterialAsset(string name,Color color)
        {
            string path="Assets/Materials/"+name+".mat";
            var material=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(material!=null)return material;
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            if(shader==null)throw new InvalidOperationException("Project's URP Lit shader could not be found.");
            material=new Material(shader){name=name,color=color};AssetDatabase.CreateAsset(material,path);return material;
        }

        private static void PrepareFont()
        {
            EnsureFolder("Assets/TextMesh Pro/Fonts");
            const string target="Assets/TextMesh Pro/Fonts/FortJapanese SDF.asset";
            if(!File.Exists(target)&&!AssetDatabase.CopyAsset(
                "Assets/TextMesh Pro/Fonts/Noto_Sans_JP/NotoSansJP-VariableFont_wght SDF.asset",target))
                throw new InvalidOperationException("Could not copy Japanese font for isolated fort UI.");
            var font=FortFont();
            font.isMultiAtlasTexturesEnabled=true;
            // Populate this new font only; no glyph or material changes reach the shared HUD font.
            string missing;
            font.TryAddCharacters(string.Join("",Titles.Concat(EarlyStageTitles))+
                "鍵謎解き解決済み出口へ向かおう順序が違います最初からやり直そう",out missing);
            EditorUtility.SetDirty(font);
            AssetDatabase.SaveAssetIfDirty(font);
        }

        private static TMP_FontAsset FortFont()
        {return AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Fonts/FortJapanese SDF.asset");}

        private static GameObject Instantiate(string path,Transform parent,Vector3 position,Vector3 scale)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if(prefab==null)throw new InvalidOperationException("Missing asset: "+path);
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent);
            go.transform.position=position;go.transform.rotation=Quaternion.identity;go.transform.localScale=scale;
            return go;
        }

        private static void BoxFromMesh(GameObject go,bool obstacle)
        {
            var collider=go.GetComponent<BoxCollider>();if(collider==null)collider=go.AddComponent<BoxCollider>();
            var filter=go.GetComponent<MeshFilter>();
            if(filter!=null&&filter.sharedMesh!=null)
            {
                var bounds=filter.sharedMesh.bounds;collider.center=bounds.center;
                collider.size=new Vector3(Mathf.Max(bounds.size.x,0.15f),Mathf.Max(bounds.size.y,0.15f),Mathf.Max(bounds.size.z,0.15f));
            }
            if(obstacle)go.layer=LayerMask.NameToLayer("Obstacle");
        }
        private static Transform Child(Transform parent,string name)
        {var go=new GameObject(name);go.transform.SetParent(parent,false);return go.transform;}
        private static void ClearChildren(Transform parent)
        {for(int i=parent.childCount-1;i>=0;i--)Object.DestroyImmediate(parent.GetChild(i).gameObject);}
        private static void EnsureFolder(string path)
        {
            if(AssetDatabase.IsValidFolder(path))return;
            var parent=Path.GetDirectoryName(path).Replace('\\','/');EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent,Path.GetFileName(path));
        }

        private const float Cell = 3f;
        private static readonly string[] EarlyStageTitles =
        { "03  |  石畳の地下道", "04  |  水没した回廊", "05  |  三つ首の砦" };

        // =====================================================
        // ダンジョンの間取り
        // 配列の0行目が最北（z = TopZ）、左端が x = LeftX
        // 'S' スタート  'G' ゴール  'K' 鍵  'E' 敵  'T' 罠  'C' 宝箱
        // '.' 床  '#' 壁
        // =====================================================
        private const int LeftX = -7;
        private const int TopZ = 12;

        private static readonly string[] Map3 =
        {
            "#######G#######",  // z=12  ゴール（扉1マス分）
            "#.....#...#...#",  // z=11
            "#..K..#.E.#.K.#",  // z=10  北西 / 北中央 / 北東
            "#.....#...#...#",  // z=9
            "#..E..#...#.E.#",  // z=8
            "#..##.....##..#",  // z=7
            "#.............#",  // z=6   北の横通路
            "#.....#...#...#",  // z=5
            "#..E..#.C.#...#",  // z=4
            "#.....#...#.K.#",  // z=3   南西 / 南中央 / 南東
            "#..##.#...#...#",  // z=2
            "#.....#.E.#.E.#",  // z=1
            "#.............#",  // z=0   南の横通路
            "######...######",  // z=-1
            "######.S.######",  // z=-2  スタート
            "######...######",  // z=-3
        };

        private static readonly string[] Map4 =
        {
            "#######G#######",  // z=12  ゴール（扉1マス分）
            "#...#.....#...#",  // z=11
            "#.K.#..E..#.E.#",  // z=10  北西 / 中央大部屋 / 北東
            "#...#.....#...#",  // z=9
            "#.#.#..C..#.#.#",  // z=8   柱あり
            "#...#.....#.K.#",  // z=7
            "#.............#",  // z=6   横通路
            "#...#.....#...#",  // z=5
            "#.E.#..T..#...#",  // z=4
            "#...#.....#.E.#",  // z=3
            "#.#.#..E..#.#.#",  // z=2
            "#.K.#.....#...#",  // z=1   南西 / 中央 / 南東
            "#.............#",  // z=0   横通路
            "######...######",  // z=-1
            "######.S.######",  // z=-2  スタート
            "######...######",  // z=-3
        };

        private static string[] MapOf(int stage) { return stage == 3 ? Map3 : Map4; }

        private static char CellAt(int stage, int x, int z)
        {
            var map = MapOf(stage);
            int row = TopZ - z;
            int col = x - LeftX;
            if (row < 0 || row >= map.Length) return '#';
            if (col < 0 || col >= map[row].Length) return '#';
            return map[row][col];
        }

        private static bool IsFloor(int stage, int x, int z) { return CellAt(stage, x, z) != '#'; }

        /// <summary>指定の記号があるマスの座標を集める。</summary>
        private static List<Vector3> Find(int stage, char symbol)
        {
            var list = new List<Vector3>();
            var map = MapOf(stage);
            for (int row = 0; row < map.Length; row++)
                for (int col = 0; col < map[row].Length; col++)
                    if (map[row][col] == symbol)
                        list.Add(new Vector3((col + LeftX) * Cell, 0, (TopZ - row) * Cell));
            return list;
        }

        /// <summary>床タイル。Stage1・2 と同じ Cave 系を使う。</summary>
        private static string FloorTile(int x, int z, int stage)
        {
            return "Assets/Prefabs/" + ((x + z + stage) % 4 == 0 ? "Cave02_4" : "Cave01_4") + ".prefab";
        }

        private static void BuildEarlyStageCopy(int stage, Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            var rooms = roots.First(g => g.name == "Rooms");
            var enemies = roots.First(g => g.name == "Enemies");
            var keysRoot = roots.First(g => g.name == "Keys");
            ClearChildren(rooms.transform);
            ClearChildren(enemies.transform);
            ClearChildren(keysRoot.transform);
            foreach (var e in enemies.GetComponents<Enemy>()) Object.DestroyImmediate(e);

            var extension = new GameObject("FortStage" + stage);

            var camera = roots.First(g => g.name == "Main Camera");
            camera.transform.position = new Vector3(0, 11, -20);
            var cameraSettings = new SerializedObject(camera.GetComponent<CameraController>());
            cameraSettings.FindProperty("fixedY").floatValue = 11;
            cameraSettings.ApplyModifiedPropertiesWithoutUndo();

            var sun = new GameObject("Fort Sun", typeof(Light));
            sun.transform.SetParent(extension.transform);
            sun.transform.rotation = Quaternion.Euler(52, -28, 0);
            var light = sun.GetComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.0f;
            light.color = stage == 4 ? new Color(0.8f, 0.93f, 1f)
                : stage == 3 ? new Color(1f, 0.93f, 0.8f) : new Color(1, 0.95f, 0.83f);
            light.shadows = LightShadows.Soft;
            // Keep this light out of the procedural skybox's automatic sun selection.
            light.renderMode = LightRenderMode.ForceVertex;
            RenderSettings.ambientLight = new Color(0.48f, 0.52f, 0.58f);
            RenderSettings.fog = false;

            var palette = new[] { new Color(0.72f, 0.6f, 0.35f),
                new Color(0.2f, 0.62f, 0.52f), new Color(0.5f, 0.4f, 0.7f) };
            Material accent = MaterialAsset("Accent" + stage, palette[stage - 3]);
            Material gold = MaterialAsset("Gold", new Color(1, 0.73f, 0.16f));

            var player = roots.First(g => g.name == "Player").GetComponent<PlayerController2>();
            var chest = roots.First(g => g.name == "TreasureChest");
            var chestComponents = chest.GetComponents<TreasureChest>();
            for (int i = 1; i < chestComponents.Length; i++) Object.DestroyImmediate(chestComponents[i]);
            var goal = roots.First(g => g.GetComponent<Door>() != null).GetComponent<Door>();

            GameObject[] keys;

            if (stage == 5)
            {
                BuildTerrain5(stage, rooms.transform);
                player.transform.SetPositionAndRotation(new Vector3(0, 0.05f, -12), Quaternion.identity);
                chest.transform.position = P(0, 6) + Vector3.up * 0.1f;
                PlaceGoal(goal, 31.5f);
                keys = new[] { NewKey(keysRoot.transform, 1, P(-13, 15)),
                               NewKey(keysRoot.transform, 2, P(13, 15)),
                               NewKey(keysRoot.transform, 3, P(0, 24)) };
                var mobs = new List<Enemy>();
                mobs.Add(Mob(enemies.transform, 0, P(-10, 9)));
                mobs.Add(Mob(enemies.transform, 2, P(10, 9)));
                mobs.Add(Mob(enemies.transform, 1, P(-10, 21)));
                mobs.Add(Mob(enemies.transform, 4, P(10, 21)));

                Trap(extension.transform, P(-3, 3), stage, 0f, accent);
                Trap(extension.transform, P(3, 3), stage, 1.6f, accent);
                Trap(extension.transform, P(0, 18), stage, 2.4f, accent);
            }
            else
            {
                BuildEarlyTerrain(stage, rooms.transform);

                var start = Find(stage, 'S').First();
                player.transform.SetPositionAndRotation(start + Vector3.up * 0.05f, Quaternion.identity);

                var goalCell = Find(stage, 'G').First();
                PlaceGoal(goal, goalCell.z);

                var chestCell = Find(stage, 'C');
                chest.transform.position = (chestCell.Count > 0 ? chestCell[0] : P(0, 6)) + Vector3.up * 0.1f;

                var keyCells = Find(stage, 'K');
                keys = new GameObject[3];
                for (int i = 0; i < 3; i++)
                    keys[i] = NewKey(keysRoot.transform, i + 1,
                        i < keyCells.Count ? keyCells[i] : P(0, 6));

                var enemyCells = Find(stage, 'E');
                int[] visuals = stage == 3
                    ? new[] { 0, 0, 1, 0, 2, 0 }
                    : new[] { 0, 2, 0, 1, 0, 0 };
                for (int i = 0; i < enemyCells.Count; i++)
                    Mob(enemies.transform, visuals[i % visuals.Length], enemyCells[i]);

                var trapCells = Find(stage, 'T');
                for (int i = 0; i < trapCells.Count; i++)
                    Trap(extension.transform, trapCells[i], stage, i * 1.5f, accent);
            }

            var objective = roots.First(g => g.name == "GameManager").GetComponent<GameManager>();
            objective.stageTitle = EarlyStageTitles[stage - 3];
            objective.keys = keys;
            objective.goal = goal;

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene))
                throw new InvalidOperationException("保存できませんでした: " + scene.path);
        }

        private static void PlaceGoal(Door goal, float z)
        {
            goal.transform.position = new Vector3(0, 0, z);
            goal.transform.rotation = Quaternion.Euler(0, 90, 0);
            goal.gameObject.layer = LayerMask.NameToLayer("Obstacle");
            var collider = goal.GetComponent<BoxCollider>();
            if (collider == null) collider = goal.gameObject.AddComponent<BoxCollider>();
            collider.center = new Vector3(0, 1, 0);
            collider.size = new Vector3(1, 3, 3 / Mathf.Abs(goal.transform.lossyScale.z));
            collider.isTrigger = false;
            PrefabUtility.RecordPrefabInstancePropertyModifications(collider);
        }

        private static void BuildEarlyTerrain(int stage, Transform parent)
        {
            var grounds = Child(parent, "Grounds");
            var walls = Child(parent, "DungeonWalls");
            var map = MapOf(stage);
            int bottomZ = TopZ - map.Length + 1;
            int rightX = LeftX + map[0].Length - 1;

            for (int x = LeftX; x <= rightX; x++)
                for (int z = bottomZ; z <= TopZ; z++)
                {
                    if (!IsFloor(stage, x, z)) continue;
                    var floor = Instantiate(FloorTile(x, z, stage), grounds,
                        P(x * Cell, z * Cell), Vector3.one * Cell);
                    floor.name = "FortFloor_" + x + "_" + z;
                    BoxFromMesh(floor, false);
                }

            for (int x = LeftX - 1; x <= rightX + 1; x++)
                for (int z = bottomZ - 1; z <= TopZ + 1; z++)
                {
                    if (IsFloor(stage, x, z)) continue;
                    if (z == TopZ + 1 && x == 0) continue;   // ゴールの開口部（扉1マス分）
                    bool nextToFloor = false;
                    for (int dx = -1; dx <= 1 && !nextToFloor; dx++)
                        for (int dz = -1; dz <= 1 && !nextToFloor; dz++)
                            if (IsFloor(stage, x + dx, z + dz)) nextToFloor = true;
                    if (!nextToFloor) continue;
                    var wall = Instantiate("Assets/Prefabs/Cave_Field_02.prefab", walls,
                        P(x * Cell, z * Cell) + Vector3.up * 1.8f, Vector3.one * Cell);
                    wall.name = "Wall_" + x + "_" + z;
                    BoxFromMesh(wall, true);
                }

            string prefab = "Assets/Prefabs/FortLayout" + stage + ".prefab";
            if (!File.Exists(prefab))
                PrefabUtility.SaveAsPrefabAssetAndConnect(parent.gameObject, prefab, InteractionMode.AutomatedAction);
        }

        private static void BuildTerrain5(int stage, Transform parent)
        {
            var grounds = Child(parent, "Grounds");
            var walls = Child(parent, "DungeonWalls");
            var landmarks = Child(parent, "FortLandmarks");
            var cells = new HashSet<Vector2Int>();
            for (int x = -5; x <= 5; x++)
                for (int z = -5; z <= 10; z++)
                {
                    bool floor = (Math.Abs(x) >= 3) || (Math.Abs(x) <= 1)
                              || (z >= -5 && z <= -4) || (z >= 7 && z <= 8);
                    if (floor) cells.Add(new Vector2Int(x, z));
                }
            for (int z = -5; z <= 10; z++) cells.Add(new Vector2Int(0, z));
            foreach (var c in cells.OrderBy(c => c.y).ThenBy(c => c.x))
            {
                var floor = Instantiate(FloorTile(c.x, c.y, stage), grounds,
                    P(c.x * Cell, c.y * Cell), Vector3.one * Cell);
                floor.name = "FortFloor_" + c.x + "_" + c.y;
                BoxFromMesh(floor, false);
            }
            for (int x = -6; x <= 6; x++)
                for (int z = -6; z <= 11; z++)
                {
                    if (x != -6 && x != 6 && z != -6 && z != 11) continue;
                    if (x == 0 && z == 11) continue;
                    var boundary = Instantiate("Assets/Prefabs/Cave_Field_02.prefab", walls,
                        P(x * Cell, z * Cell) + Vector3.up * 1.8f, Vector3.one * Cell);
                    boundary.name = "Boundary_" + x + "_" + z;
                    BoxFromMesh(boundary, true);
                }
            foreach (var p in new[] { P(-13, 9), P(13, 9), P(-13, 24), P(13, 24) })
            {
                var cover = Instantiate(Fort + "Side/Fort_Stone_12.prefab", landmarks, p, Vector3.one * 2);
                BoxFromMesh(cover, true);
            }
            string prefab = "Assets/Prefabs/FortLayout" + stage + ".prefab";
            if (!File.Exists(prefab))
                PrefabUtility.SaveAsPrefabAssetAndConnect(parent.gameObject, prefab, InteractionMode.AutomatedAction);
        }

    }
}
