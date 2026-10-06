using UnityEngine;
using System.Collections.Generic;
using UnityEngine.InputSystem;
using TMPro;
using StarterAssets;
using UnityEngine.UI;
using System.Collections;

public class PlayerController2 : MonoBehaviour
{
    [Header("Movement")]
    public float speed = 5f;
    public float turnSpeed = 720f;

    [Header("Pickup")]
    public int maxpickup = 0;
    public TextMeshProUGUI countText;
    public GameObject winTextObject;

    [Header("Player Status")]
    public float maxHp = 4f;
    public float hp = 4f;
    public int EnemiesDefeated { get; private set; }

    // 10体撃破で全回復スキル
    private bool hasTenKillHeal = false;
    private int killsSinceLastHeal = 0;

    [Header("Heart UI")]
    public Image[] hearts;

    // 最大HPアップで追加するハート
    public Image heartPrefab;

    // ハートを生成する親オブジェクト
    public Transform heartParent;

    // 実際に使用する全ハート
    private List<Image> allHearts = new List<Image>();

    [Header("Player Model")]
    public Transform modelRoot;

    private Rigidbody rb;
    private StarterAssetsInputs input;
    private Animator animator;
    private int count;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private bool hasOneTimeRevive;

    // =========================================================
    // Attack
    // =========================================================

    [Header("Attack")]
    public float attackRange = 2f;
    public float attackRadius = 1.5f;
    public float attackDamage = 1f;
    public float attackCooldown = 0.5f;
    public LayerMask enemyLayer;

    [Header("Ammo")]
    public int maxAmmo = 3;
    public int currentAmmo = 3;
    public float reloadTime = 2f;

    // リロード時間を短縮する
    public void ReduceReloadTime(float amount)
    {
        reloadTime = Mathf.Max(0.1f, reloadTime - amount);

        Debug.Log("リロード時間: " + reloadTime + "秒");
    }

    // =========================================================
    // Ammo UI
    // =========================================================

    [Header("Ammo UI")]
    public Image[] ammoImages;

    // 弾UI全体
    public RectTransform ammoUI;

    // プレイヤー頭上からの高さ
    public float ammoUIHeight = 0f;

    // UIを表示するカメラ
    public Camera mainCamera;

    // =========================================================
    // Reload UI
    // =========================================================

    [Header("Reload UI")]
    public GameObject reloadUI;

    // リロード残り時間
    public TextMeshProUGUI reloadText;

    // =========================================================
    // Ammo Color
    // =========================================================

    // 弾が残っているとき
    private readonly Color ammoFullColor =
        new Color32(255, 255, 255, 255);

    // 使用済みの弾
    private readonly Color ammoEmptyColor =
        new Color32(200, 200, 200, 200);

    private bool canAttack = true;
    private bool isReloading = false;

    private static readonly int SpeedHash =
        Animator.StringToHash("Speed");

    private static readonly int DoAttackHash =
        Animator.StringToHash("DoAttack");

    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        input = GetComponent<StarterAssetsInputs>();
        animator = GetComponentInChildren<Animator>();

        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        spawnPosition = transform.position;
        spawnRotation = transform.rotation;

        rb.constraints =
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        rb.angularVelocity = Vector3.zero;

        count = 0;

        if (winTextObject != null)
        {
            winTextObject.SetActive(false);
        }

        // リロードUIは最初は非表示
        if (reloadUI != null)
        {
            reloadUI.SetActive(false);
        }

        SetCountText();

        // =====================================================
        // Heart UI 初期化
        // =====================================================

        allHearts.Clear();

        // Inspectorに設定されている最初のハートを登録
        if (hearts != null)
        {
            foreach (Image heart in hearts)
            {
                if (heart != null && !allHearts.Contains(heart))
                {
                    allHearts.Add(heart);
                }
            }
        }

        // 最大HPに必要なハート数を確保
        EnsureHeartCount(Mathf.CeilToInt(maxHp));

        UpdateHearts();

        // 弾数を初期化
        currentAmmo = maxAmmo;
        UpdateAmmoUI();
    }

    // =========================================================
    // Movement
    // =========================================================

    private void FixedUpdate()
    {
        if (input == null || rb == null)
        {
            return;
        }

        Vector2 moveInput = input.move;

        Vector3 movement = new Vector3(
            moveInput.x,
            0f,
            moveInput.y
        );

        movement = Vector3.ClampMagnitude(movement, 1f);

        Vector3 currentVelocity = rb.linearVelocity;

        rb.linearVelocity = new Vector3(
            movement.x * speed,
            currentVelocity.y,
            movement.z * speed
        );

        rb.angularVelocity = Vector3.zero;

        if (movement.sqrMagnitude > 0.01f && modelRoot != null)
        {
            Quaternion targetRotation =
                Quaternion.LookRotation(
                    movement.normalized,
                    Vector3.up
                );

            modelRoot.rotation =
                Quaternion.RotateTowards(
                    modelRoot.rotation,
                    targetRotation,
                    turnSpeed * Time.fixedDeltaTime
                );
        }

        if (animator != null)
        {
            animator.SetFloat(
                SpeedHash,
                movement.magnitude,
                0.1f,
                Time.fixedDeltaTime
            );
        }
    }

    // =========================================================
    // Attack Input
    // =========================================================

    public void OnAttack(InputValue value)
    {
        if (!value.isPressed)
        {
            return;
        }

        Attack();
    }

    // =========================================================
    // Attack Button
    // =========================================================

    public void AttackButton()
    {
        Attack();
    }

    // =========================================================
    // Attack
    // =========================================================

    void Attack()
    {
        // 攻撃不可・停止中・死亡中・リロード中・弾切れ
        if (!canAttack ||
            Time.timeScale <= 0f ||
            IsDead() ||
            isReloading ||
            currentAmmo <= 0)
        {
            return;
        }

        if (animator == null)
        {
            return;
        }

        // 弾を1発消費
        currentAmmo--;
        UpdateAmmoUI();

        // 攻撃クールタイム開始
        canAttack = false;

        // アニメーション
        animator.ResetTrigger(DoAttackHash);
        animator.SetTrigger(DoAttackHash);

        // 攻撃方向
        Vector3 attackDirection =
            modelRoot != null
                ? modelRoot.forward
                : transform.forward;

        attackDirection.y = 0f;

        if (attackDirection.sqrMagnitude <= 0.0001f)
        {
            attackDirection = transform.forward;
            attackDirection.y = 0f;
        }

        attackDirection.Normalize();

        Vector3 attackOrigin =
            transform.position + Vector3.up * 0.7f;

        Vector3 attackPos =
            attackOrigin + attackDirection * attackRange;

        // 攻撃判定
        Collider[] hits = Physics.OverlapCapsule(
            attackOrigin,
            attackPos,
            attackRadius,
            enemyLayer,
            QueryTriggerInteraction.Ignore
        );

        // 同じEnemyに複数Colliderがあっても1回だけダメージ
        var damagedEnemies = new HashSet<Enemy>();

        foreach (Collider hit in hits)
        {
            Enemy enemy =
                hit.GetComponentInParent<Enemy>();

            if (enemy == null || !damagedEnemies.Add(enemy))
            {
                continue;
            }

            // プレイヤーの後方にいる敵を除外
            Vector3 toEnemy =
                enemy.transform.position - transform.position;

            toEnemy.y = 0f;

            if (Vector3.Dot(attackDirection, toEnemy) < -0.1f)
            {
                continue;
            }

            // 障害物越しの攻撃を除外
            if (Physics.Linecast(
                attackOrigin,
                hit.bounds.center,
                LayerMask.GetMask("Obstacle"),
                QueryTriggerInteraction.Ignore
            ))
            {
                continue;
            }

            if (enemy.TakeDamage(attackDamage))
            {
                RegisterEnemyDefeated();
            }

            Vector3 knockbackDirection =
                enemy.transform.position - transform.position;

            enemy.Knockback(knockbackDirection);
        }

        // 弾切れならクールタイム後にリロード
        if (currentAmmo <= 0)
        {
            Invoke(nameof(StartReload), attackCooldown);
        }
        else
        {
            Invoke(nameof(ResetAttack), attackCooldown);
        }
    }

    // =========================================================
    // Attack Cooldown
    // =========================================================

    void ResetAttack()
    {
        canAttack = true;
    }

    // =========================================================
    // Reload
    // =========================================================

    void StartReload()
    {
        if (isReloading)
        {
            return;
        }

        isReloading = true;
        canAttack = false;

        Debug.Log("リロード開始");

        // リロードUIを表示
        if (reloadUI != null)
        {
            reloadUI.SetActive(true);
        }

        // リロード開始時の表示
        if (reloadText != null)
        {
            reloadText.text =
                "リロード中... " +
                reloadTime.ToString("0.0") +
                "秒";
        }

        StartCoroutine(ReloadCoroutine());
    }

    IEnumerator ReloadCoroutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < reloadTime)
        {
            elapsedTime += Time.deltaTime;

            float remainingTime =
                Mathf.Max(
                    0f,
                    reloadTime - elapsedTime
                );

            if (reloadText != null)
            {
                reloadText.text =
                    "リロード中... " +
                    remainingTime.ToString("0.0") +
                    "秒";
            }

            yield return null;
        }

        FinishReload();
    }

    void FinishReload()
    {
        // 弾を3発に戻す
        currentAmmo = maxAmmo;

        isReloading = false;
        canAttack = true;

        // 弾を全部白色・完全不透明に戻す
        UpdateAmmoUI();

        if (reloadText != null)
        {
            reloadText.text = "";
        }

        // リロードUIを非表示
        if (reloadUI != null)
        {
            reloadUI.SetActive(false);
        }

        Debug.Log("リロード完了");
    }

    // =========================================================
    // Ammo UI
    // =========================================================

    void UpdateAmmoUI()
    {
        if (ammoImages == null)
        {
            return;
        }

        for (int i = 0; i < ammoImages.Length; i++)
        {
            if (ammoImages[i] == null)
            {
                continue;
            }

            // 弾は常に表示する
            ammoImages[i].enabled = true;

            // 残弾なら白色・完全不透明
            if (i < currentAmmo)
            {
                ammoImages[i].color =
                    ammoFullColor;
            }
            // 使用済みなら C8C8C8・透明度200
            else
            {
                ammoImages[i].color =
                    ammoEmptyColor;
            }
        }
    }

    // =========================================================
    // Pickup
    // =========================================================

    private void SetCountText()
    {
        if (countText != null)
        {
            countText.text = "Count: " + count;
        }

        if (
            maxpickup > 0 &&
            count >= maxpickup &&
            winTextObject != null
        )
        {
            winTextObject.SetActive(true);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("PickUp"))
        {
            other.gameObject.SetActive(false);
            count++;
            SetCountText();
        }
    }

    // =========================================================
    // HP
    // =========================================================

    public void TakeDamage(float damage)
    {
        hp -= damage;

        if (hp < 0)
        {
            hp = 0;
        }

        UpdateHearts();

        if (hp <= 0)
        {
            if (!TryReviveAtSpawn())
            {
                GameOver();
            }
        }
    }

    public void HealToFull()
    {
        hp = maxHp;
        UpdateHearts();
    }

    public void AddAttackDamage(float amount)
    {
        attackDamage += amount;
    }

    public void AddSpeed(float amount)
    {
        speed += amount;
    }

    // =========================================================
    // 最大HPアップ
    // =========================================================

    public void AddMaxHp(float amount)
    {
        maxHp += amount;

        if (hp > maxHp)
        {
            hp = maxHp;
        }

        // 最大HPに合わせてハートを自動生成
        EnsureHeartCount(Mathf.CeilToInt(maxHp));

        UpdateHearts();

        Debug.Log(
            "最大HPが " + maxHp + " になりました。"
        );
    }

    // =========================================================
    // Heart UI
    // =========================================================

    private void EnsureHeartCount(int requiredCount)
    {
        // すでに必要数があるなら何もしない
        if (allHearts.Count >= requiredCount)
        {
            return;
        }

        if (heartPrefab == null)
        {
            Debug.LogError(
                "Heart PrefabがPlayerController2に設定されていません。"
            );

            return;
        }

        if (heartParent == null)
        {
            Debug.LogError(
                "Heart ParentがPlayerController2に設定されていません。"
            );

            return;
        }

        while (allHearts.Count < requiredCount)
        {
            Image newHeart =
                Instantiate(
                    heartPrefab,
                    heartParent
                );

            newHeart.gameObject.SetActive(true);

            allHearts.Add(newHeart);

            Debug.Log(
                "ハートを追加しました。現在の数: " +
                allHearts.Count
            );
        }
    }

    private void UpdateHearts()
    {
        int requiredHeartCount =
            Mathf.CeilToInt(maxHp);

        // 最大HPに必要なハート数を確保
        EnsureHeartCount(requiredHeartCount);

        for (int i = 0; i < allHearts.Count; i++)
        {
            if (allHearts[i] == null)
            {
                continue;
            }

            // 最大HPを超えるハートは非表示
            if (i >= requiredHeartCount)
            {
                allHearts[i].gameObject.SetActive(false);
                continue;
            }

            allHearts[i].gameObject.SetActive(true);

            // 現在HPがあるハート
            if (i < hp)
            {
                allHearts[i].color =
                    Color.white;
            }
            // HPがないハート
            else
            {
                allHearts[i].color =
                    new Color(
                        0.3f,
                        0.3f,
                        0.3f,
                        1f
                    );
            }
        }
    }

    // =========================================================
    // One Time Revive
    // =========================================================

    public void EnableOneTimeRevive()
    {
        hasOneTimeRevive = true;
    }

    public bool TryReviveAtSpawn()
    {
        if (!hasOneTimeRevive)
        {
            return false;
        }

        hasOneTimeRevive = false;
        hp = maxHp;

        transform.SetPositionAndRotation(
            spawnPosition,
            spawnRotation
        );

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        UpdateHearts();

        Time.timeScale = 1f;

        Debug.Log(
            "復活スキルで初期位置に戻りました。"
        );

        return true;
    }

    // =========================================================
    // Enemy
    // =========================================================

    public void RegisterEnemyDefeated()
    {
        EnemiesDefeated++;

        PlayerSkills skills =
            GetComponent<PlayerSkills>();

        if (skills != null)
        {
            skills.OnEnemyDefeated();
        }
    }

    // =========================================================
    // 10体撃破で全回復スキルを有効化
    // =========================================================

    public void EnableTenKillHeal()
    {
        hasTenKillHeal = true;
        killsSinceLastHeal = 0;

        Debug.Log(
            "10体撃破で全回復スキルを取得しました。"
        );
    }

    // =========================================================
    // スキル用の敵撃破処理
    // =========================================================

    public void OnEnemyDefeatedForSkill()
    {
        if (!hasTenKillHeal)
        {
            return;
        }

        killsSinceLastHeal++;

        Debug.Log(
            "スキル用撃破数: " +
            killsSinceLastHeal +
            "/10"
        );

        if (killsSinceLastHeal >= 10)
        {
            HealToFull();

            killsSinceLastHeal = 0;

            Debug.Log(
                "敵を10体撃破したため体力全回復！"
            );
        }
    }

    // =========================================================
    // Game Over
    // =========================================================

    void GameOver()
    {
        Debug.Log("Game Over");

        Time.timeScale = 0f;

        // gameOverUI.SetActive(true);
    }

    public bool IsDead()
    {
        return hp <= 0;
    }

    // =========================================================
    // Gizmos
    // =========================================================

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 attackDirection =
            modelRoot != null
                ? modelRoot.forward
                : transform.forward;

        attackDirection.y = 0f;

        if (attackDirection.sqrMagnitude <= 0.0001f)
        {
            attackDirection = transform.forward;
            attackDirection.y = 0f;
        }

        attackDirection.Normalize();

        Vector3 attackOrigin =
            transform.position + Vector3.up * 0.7f;

        Vector3 attackPos =
            attackOrigin + attackDirection * attackRange;

        Gizmos.DrawWireSphere(
            attackOrigin,
            attackRadius
        );

        Gizmos.DrawWireSphere(
            attackPos,
            attackRadius
        );
    }

    // =========================================================
    // Ammo UI Position
    // =========================================================

    private void LateUpdate()
    {
        UpdateAmmoUIPosition();
    }

    void UpdateAmmoUIPosition()
    {
        if (ammoUI == null || mainCamera == null)
        {
            return;
        }

        // プレイヤーの頭上のワールド座標
        Vector3 worldPosition =
            transform.position +
            Vector3.up * ammoUIHeight;

        // ワールド座標 → スクリーン座標
        Vector3 screenPosition =
            mainCamera.WorldToScreenPoint(
                worldPosition
            );

        // プレイヤーの後ろにいる場合は非表示
        if (screenPosition.z < 0)
        {
            ammoUI.gameObject.SetActive(false);
            return;
        }

        ammoUI.gameObject.SetActive(true);

        // Screen Space - Overlay Canvas
        ammoUI.position = screenPosition;
    }

    // =========================================================
    // 視野拡大スキル
    // =========================================================

    public void IncreaseCameraHeight(float amount)
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
        }

        if (mainCamera == null)
        {
            Debug.LogWarning(
                "Main Cameraが見つかりません。"
            );

            return;
        }

        Vector3 cameraPosition =
            mainCamera.transform.position;

        cameraPosition.y += amount;

        mainCamera.transform.position =
            cameraPosition;

        Debug.Log(
            "カメラY座標 +" + amount
        );
    }
}