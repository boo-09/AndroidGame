using UnityEngine;

// クリア時のステータスとスキルを保存して、次のステージに引き継ぐ
public class PlayerStatusCarrier : MonoBehaviour
{
    const string KEY_HAS_DATA = "Carry_HasData";
    const string KEY_HP = "Carry_Hp";
    const string KEY_MAX_HP = "Carry_MaxHp";
    const string KEY_SPEED = "Carry_Speed";
    const string KEY_ATTACK = "Carry_Attack";
    const string KEY_RELOAD = "Carry_Reload";
    const string KEY_CAMERA_Y = "Carry_CameraY";
    const string KEY_SKILLS = "Carry_Skills";

    PlayerController2 controller;
    PlayerSkills skills;

    void Awake()
    {
        controller = GetComponent<PlayerController2>();
        skills = GetComponent<PlayerSkills>();
    }

    void Start()
    {
        Load();
    }

    // ステージ開始時に、保存された値を反映する
    void Load()
    {
        if (PlayerPrefs.GetInt(KEY_HAS_DATA, 0) == 0)
        {
            Debug.Log("PlayerStatusCarrier: 引き継ぎデータなし（初期状態で開始）");
            return;
        }

        if (controller != null)
        {
            controller.maxHp = PlayerPrefs.GetFloat(KEY_MAX_HP, controller.maxHp);
            controller.hp = PlayerPrefs.GetFloat(KEY_HP, controller.hp);
            controller.speed = PlayerPrefs.GetFloat(KEY_SPEED, controller.speed);
            controller.attackDamage = PlayerPrefs.GetFloat(KEY_ATTACK, controller.attackDamage);
            controller.reloadTime = PlayerPrefs.GetFloat(KEY_RELOAD, controller.reloadTime);

            // 念のため、HPが0以下で引き継がれた場合は全回復させる
            if (controller.hp <= 0f)
            {
                controller.hp = controller.maxHp;
                Debug.LogWarning("PlayerStatusCarrier: HPが0だったため全回復しました");
            }
        }

        // カメラのY座標（視野拡大スキル）
        CameraController cam = FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            float savedY = PlayerPrefs.GetFloat(KEY_CAMERA_Y, cam.FixedY);
            cam.AddFixedY(savedY - cam.FixedY);
        }

        // 取得済みスキル
        if (skills != null)
        {
            skills.RestoreAcquiredSkills(PlayerPrefs.GetString(KEY_SKILLS, ""));
        }

        if (controller != null)
        {
            Debug.Log($"PlayerStatusCarrier: 引き継ぎ HP={controller.hp}/{controller.maxHp} 攻撃={controller.attackDamage} 速度={controller.speed}");

            // ハートなどの表示を更新させる
            controller.SendMessage("UpdateHearts", SendMessageOptions.DontRequireReceiver);
        }
    }

    // クリア時に呼ぶ
    public void Save()
    {
        if (controller == null) return;

        // HPが0以下のときは保存しない（ゲームオーバー状態を引き継がないため）
        if (controller.hp <= 0f)
        {
            Debug.Log("PlayerStatusCarrier: HPが0のため保存しません");
            return;
        }

        PlayerPrefs.SetInt(KEY_HAS_DATA, 1);
        PlayerPrefs.SetFloat(KEY_HP, controller.hp);
        PlayerPrefs.SetFloat(KEY_MAX_HP, controller.maxHp);
        PlayerPrefs.SetFloat(KEY_SPEED, controller.speed);
        PlayerPrefs.SetFloat(KEY_ATTACK, controller.attackDamage);
        PlayerPrefs.SetFloat(KEY_RELOAD, controller.reloadTime);

        CameraController cam = FindFirstObjectByType<CameraController>();
        if (cam != null)
        {
            PlayerPrefs.SetFloat(KEY_CAMERA_Y, cam.FixedY);
        }

        if (skills != null)
        {
            PlayerPrefs.SetString(KEY_SKILLS, skills.GetAcquiredSkillsString());
        }

        PlayerPrefs.Save();
        Debug.Log($"PlayerStatusCarrier: ステータスを保存しました HP={controller.hp}/{controller.maxHp}");
    }

    // ゲームオーバー時に呼ぶ
    public void Clear()
    {
        PlayerPrefs.DeleteKey(KEY_HAS_DATA);
        PlayerPrefs.DeleteKey(KEY_HP);
        PlayerPrefs.DeleteKey(KEY_MAX_HP);
        PlayerPrefs.DeleteKey(KEY_SPEED);
        PlayerPrefs.DeleteKey(KEY_ATTACK);
        PlayerPrefs.DeleteKey(KEY_RELOAD);
        PlayerPrefs.DeleteKey(KEY_CAMERA_Y);
        PlayerPrefs.DeleteKey(KEY_SKILLS);
        PlayerPrefs.Save();
        Debug.Log("PlayerStatusCarrier: 引き継ぎデータをリセットしました");
    }
}