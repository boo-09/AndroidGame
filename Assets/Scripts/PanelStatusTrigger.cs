using UnityEngine;

// パネルが表示されたときに、引き継ぎデータを保存またはリセットする
public class PanelStatusTrigger : MonoBehaviour
{
    public enum Mode { Save, Clear }

    [Tooltip("Save=クリア画面用 / Clear=ゲームオーバー画面用")]
    [SerializeField] Mode mode = Mode.Save;

    void OnEnable()
    {
        PlayerStatusCarrier carrier = FindFirstObjectByType<PlayerStatusCarrier>();

        if (carrier == null)
        {
            Debug.LogWarning("PanelStatusTrigger: PlayerStatusCarrier が見つかりません", this);
            return;
        }

        if (mode == Mode.Save)
        {
            carrier.Save();
        }
        else
        {
            carrier.Clear();
        }
    }
}