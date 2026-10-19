using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace FortStages.Editor
{
    public static class FortStageValidator
    {
        [MenuItem("Tools/Fort Stages/Validate All Stages")]
        public static void ValidateFromMenu() { Debug.Log(ValidateAll()); }

        /// <summary>Checks saved wiring, allowed assets, and continuous walking routes after unlocking gates.</summary>
        public static string ValidateAll()
        {
            if(EditorApplication.isPlayingOrWillChangePlaymode) throw new InvalidOperationException("Stop Play mode first.");
            for(int i=0;i<SceneManager.sceneCount;i++)
                if(SceneManager.GetSceneAt(i).isDirty)throw new InvalidOperationException("Save the current scene first.");
            var setup=EditorSceneManager.GetSceneManagerSetup();
            var reports=new List<string>();
            try
            {
                for(int stage=6;stage<=10;stage++)
                {
                    var scene=EditorSceneManager.OpenScene(FortStageBuilder.SceneFolder+"/Stage"+stage+".unity");
                    var all=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).ToArray();
                    foreach(var t in all)
                    {
                        if(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)>0)
                            throw new InvalidOperationException("Missing script in "+scene.name+": "+t.name);
                        string source=PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(t.gameObject);
                        if(source.Contains("02_Cave") && !source.EndsWith("Cave_Field_02.prefab"))
                            throw new InvalidOperationException("Forbidden cave asset: "+source);
                        if(t.name.StartsWith("Boundary_CaveField02") &&
                            (Mathf.Abs(t.position.x)<17 && t.position.z>-17 && t.position.z<32))
                            throw new InvalidOperationException("Cave field inside fort interior.");
                    }
                    var objective=UnityEngine.Object.FindFirstObjectByType<GameManager>();
                    var chest=UnityEngine.Object.FindFirstObjectByType<TreasureChest>();
                    var player=UnityEngine.Object.FindFirstObjectByType<PlayerController2>();
                    var mobs=UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsSortMode.None)
                        .Where(m=>m.GetComponent<Rigidbody>()!=null).ToArray();
                    Require(objective!=null && objective.keys.Length==3 && objective.keys.All(k=>k!=null && k.GetComponent<Key>()!=null),"Three wired keys",stage);
                    Require(chest!=null&&chest.candidatePoints.Length>=3&&chest.candidatePoints.All(t=>t!=null),"Chest candidates",stage);
                    Require(UnityEngine.Object.FindObjectsByType<TreasureChest>(FindObjectsInactive.Include,FindObjectsSortMode.None).Length==1,"One chest component",stage);
                    int[] expectedCounts={3,3,2,4,5};
                    Require(mobs.Length==expectedCounts[stage-6],"Enemy count",stage);
                    Require(mobs.All(HasBaselineSettings),"Existing Enemy movement/contact attack/health settings",stage);
                    Require(mobs.All(HasBaselineRareSettings),"Existing rare enemy settings",stage);
                    Require(all.All(t=>t.GetComponents<MonoBehaviour>().All(m=>m==null||m.GetType().Name!="FortEnemyBehaviour")),"No replacement enemy AI",stage);
                    Require(mobs.All(m=>m.GetComponentsInChildren<Collider>().Any(c=>c.enabled&&!c.isTrigger)&&
                        m.GetComponentsInChildren<Collider>().All(c=>c.gameObject.layer==LayerMask.NameToLayer("Enemy"))),"Enemy colliders/layer",stage);
                    Require(mobs.All(m=>m.GetComponentInChildren<Animator>()!=null&&
                        m.GetComponentInChildren<Animator>().runtimeAnimatorController!=null&&!m.GetComponentInChildren<Animator>().applyRootMotion),"Monster animator controllers",stage);
                    Require(mobs.Any(m=>MonsterControllerName(m).Contains("Slime")||MonsterControllerName(m).Contains("Turtle")),"Existing monster",stage);
                    Require(mobs.Any(m=>MonsterControllerName(m).Contains("Cactus")||MonsterControllerName(m).Contains("Mushroom")||
                        MonsterControllerName(m).Contains("Beholder")),"New monster using existing Enemy",stage);
                    Require(objective.goal!=null&&Camera.main!=null&&UnityEngine.Object.FindFirstObjectByType<Light>()!=null,"Goal/camera/light",stage);
                    var carriers=mobs.Where(m => m.keyObject != null).ToArray();
                    Require(carriers.Length>0&&carriers.All(c=>c.keyObject!=null&&!c.keyObject.transform.IsChildOf(c.transform)),"Key carriers",stage);
                    if(objective.puzzle!=null)
                    {
                        Require(objective.puzzle.rewardKey!=null&&objective.puzzle.gate!=null,"Puzzle reward/gate",stage);
                        // Validate the walking route after solving. Nothing is saved by this check.
                        objective.puzzle.gate.SetActive(false);
                    }
                    foreach(var m in mobs) foreach(var c in m.GetComponentsInChildren<Collider>()) c.enabled=false;
                    foreach(var c in player.GetComponentsInChildren<Collider>()) c.enabled=false;
                    Physics.SyncTransforms();
                    var reachable=WalkingCells(player.transform.position);
                    var points=chest.candidatePoints.Select(p=>p.position).Concat(objective.keys.Select(k=>k.transform.position))
                        .Concat(UnityEngine.Object.FindObjectsByType<FortPuzzleSwitch>(FindObjectsSortMode.None).Select(s=>s.transform.position))
                        .Concat(new[]{objective.goal.transform.position+Vector3.back*1.3f});
                    foreach(var point in points)
                        Require(reachable.Any(c=>(GridPoint(c)-new Vector3(point.x,0,point.z)).sqrMagnitude<2.8f),"Walkable route to "+point,stage);
                    reports.Add(scene.name+": OK ("+mobs.Length+" enemies, "+chest.candidatePoints.Length+" chest candidates, "+reachable.Count+" connected walking cells)");
                }
            }
            finally { EditorSceneManager.RestoreSceneManagerSetup(setup); }
            return string.Join("\n",reports);
        }

        private static HashSet<Vector2Int> WalkingCells(Vector3 start)
        {
            var reachable=new HashSet<Vector2Int>();var pending=new Queue<Vector2Int>();
            var origin=new Vector2Int(Mathf.RoundToInt(start.x/1.5f),Mathf.RoundToInt(start.z/1.5f));
            reachable.Add(origin);pending.Enqueue(origin);
            var offsets=new[]{Vector2Int.left,Vector2Int.right,Vector2Int.up,Vector2Int.down};
            while(pending.Count>0)
            {
                var c=pending.Dequeue();
                foreach(var offset in offsets)
                {
                    var next=c+offset;
                    if(reachable.Contains(next)||next.x<-11||next.x>11||next.y<-11||next.y>21)continue;
                    var p=GridPoint(next);
                    var hits=Physics.RaycastAll(p+Vector3.up*0.7f,Vector3.down,1,~0,QueryTriggerInteraction.Ignore);
                    if(!hits.Any(h=>h.normal.y>0.75f&&h.point.y<0.15f&&h.point.y>-0.2f&&h.collider.gameObject.layer!=LayerMask.NameToLayer("Obstacle")))continue;
                    if(Physics.CheckCapsule(p+Vector3.up*0.5f,p+Vector3.up*1.3f,0.43f,LayerMask.GetMask("Obstacle"),QueryTriggerInteraction.Ignore))continue;
                    RaycastHit obstruction;
                    if(Physics.SphereCast(GridPoint(c)+Vector3.up*0.7f,0.43f,(p-GridPoint(c)).normalized,out obstruction,1.5f,
                        LayerMask.GetMask("Obstacle"),QueryTriggerInteraction.Ignore))continue;
                    reachable.Add(next);pending.Enqueue(next);
                }
            }
            return reachable;
        }
        private static Vector3 GridPoint(Vector2Int c){return new Vector3(c.x*1.5f,0,c.y*1.5f);}
        internal static bool HasBaselineSettings(Enemy enemy)
        {
            Rigidbody body=enemy.GetComponent<Rigidbody>();
            SphereCollider collider=enemy.GetComponent<SphereCollider>();
            bool isTurtle=MonsterControllerName(enemy).Contains("Turtle");
            float expectedDamage=isTurtle?2f:1f;
            return enemy.enabled&&body!=null&&body.useGravity&&!body.isKinematic&&
                body.constraints==(RigidbodyConstraints.FreezeRotationX|RigidbodyConstraints.FreezeRotationZ)&&
                body.interpolation==RigidbodyInterpolation.None&&body.collisionDetectionMode==CollisionDetectionMode.Discrete&&
                collider!=null&&collider.enabled&&!collider.isTrigger&&Mathf.Approximately(collider.radius,0.5f)&&
                (collider.center-new Vector3(0,0.5f,0)).sqrMagnitude<0.0001f&&
                enemy.gameObject.layer==LayerMask.NameToLayer("Enemy")&&
                Mathf.Approximately(enemy.speed,3f)&&Mathf.Approximately(enemy.detectionRange,10f)&&
                enemy.obstacleLayer.value==LayerMask.GetMask("Obstacle")&&Mathf.Approximately(enemy.rotateSpeed,5f)&&
                Mathf.Approximately(enemy.hp,1f)&&Mathf.Approximately(enemy.attackDamage,expectedDamage)&&
                Mathf.Approximately(enemy.attackInterval,1f)&&Mathf.Approximately(enemy.knockbackForce,25f)&&
                Mathf.Approximately(enemy.knockbackDuration,0.1f)&&Mathf.Approximately(enemy.fallDeathY,-10f)&&
                Mathf.Approximately(enemy.jumpForce,8f)&&Mathf.Approximately(enemy.chestCheckDistance,isTurtle?1.5f:2f)&&
                Mathf.Approximately(enemy.chestCheckRadius,0.5f)&&Mathf.Approximately(enemy.jumpCooldown,isTurtle?1f:0.8f);
        }
        private static string MonsterControllerName(Enemy enemy)
        {
            Animator animator=enemy.GetComponentInChildren<Animator>();
            return animator!=null&&animator.runtimeAnimatorController!=null?animator.runtimeAnimatorController.name:string.Empty;
        }
        internal static bool HasBaselineRareSettings(Enemy enemy)
        {
            RareEnemy rare=enemy.GetComponent<RareEnemy>();
            if(MonsterControllerName(enemy).Contains("Turtle"))return rare==null;
            if(rare==null||!rare.enabled)return false;
            var data=new SerializedObject(rare);
            return Mathf.Approximately(data.FindProperty("rareChance").floatValue,0.1f)&&
                Mathf.Approximately(data.FindProperty("rareScale").floatValue,1.2f)&&
                Mathf.Approximately(data.FindProperty("hpMultiplier").floatValue,1f);
        }
        private static void Require(bool success,string check,int stage)
        {if(!success)throw new InvalidOperationException("Stage"+stage+" failed: "+check);}
    }
}
