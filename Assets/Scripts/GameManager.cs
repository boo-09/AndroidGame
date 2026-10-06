using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Text.RegularExpressions;
using System.Runtime.Serialization;

public class GameManager : MonoBehaviour
{
    [Header("UI参照")]
    [SerializeField] private TextMeshProUGUI timeText;
    [UnityEngine.Serialization.FormerlySerializedAs("itemCountText")]
    [SerializeField] private TextMeshProUGUI keyText;
    [SerializeField] private GameObject gameClearPanel;
    [SerializeField] private GameObject gameOverPanel;
    [SerializeField] private TextMeshProUGUI countdownText;

    [Header("UI Controllers")]
    [SerializeField] private CharacterStatusHUD statusHUD;
    [SerializeField] private MinimapController minimapController;
    [SerializeField] private PauseMenuController pauseMenu;

    [Header("ベストタイムUI")]
    [SerializeField] private TextMeshProUGUI bestTimeText;
    [SerializeField] private TextMeshProUGUI newRecordText;
    [SerializeField] private TMP_FontAsset statusFont;

    [Header("ゲーム設定")]
    [SerializeField] private float fallThreshold = -5f;
    [SerializeField] private int maxStage = 20;

    const string NEXT_STAGE_KEY = "NextStage";
    const string BEST_TIME_KEY_PREFIX = "BestTime_";

    private const int totalItems = 3;
    private int collectedItems = 0;
    private PlayerController2 player;
    private bool isGameOver = false;
    private bool isGameStarted = false;
    private bool isPaused;
    private float currentTime = 0f;

    void Start()
    {
        // 初回起動用
        if (!PlayerPrefs.HasKey(NEXT_STAGE_KEY))
        {
            PlayerPrefs.SetInt(NEXT_STAGE_KEY, 1);
        }

        InitializeGame();
        StartCoroutine(StartCountdown());
    }

    void Update()
    {
        if (!isGameOver && isGameStarted && !isPaused)
        {
            currentTime += Time.deltaTime;
            UpdateUI();
            CheckDeath();
        }
    }

    void InitializeGame()
    {
        player = FindFirstObjectByType<PlayerController2>();

        if (player != null)
        {
            if (statusHUD != null)
            {
                statusHUD.Initialize(player,statusFont);
            }

            if (minimapController != null)
            {
                minimapController.Initialize(player.transform);
            }
        }

        if (pauseMenu != null)
        {
            pauseMenu.Initialize(this, statusFont);
        }


        UpdateUI();

        if (gameClearPanel != null) gameClearPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (newRecordText != null) newRecordText.gameObject.SetActive(false);

        Time.timeScale = 1f;
        currentTime = 0f;

        UpdateBestTimeUI();
    }

    public void ItemCollected()
    {
        collectedItems = Mathf.Min(collectedItems + 1, totalItems);
        UpdateUI();
    }

    public void TryGoal()
    {
        if (isGameStarted && !isGameOver && !isPaused && collectedItems >= totalItems)
        {
            GameClear();
        }
    }

    public void RestartGame()
    {
        SceneManager.LoadScene("Stage1");
    }

    void CheckDeath()
    {
        if (player == null) return;

        if (player.transform.position.y < fallThreshold)
        {
            if (!player.TryReviveAtSpawn())
            {
                GameOver();
            }
        }
        else if (player.IsDead())
        {
            GameOver();
        }
    }

    void GameClear()
    {
        if (isGameOver) return; // すでにクリア/ゲームオーバー済みなら何もしない
        isGameOver = true;

        UpdateBestTime();

        if (gameClearPanel != null)
        {
            gameClearPanel.SetActive(true);
        }

        // ★次のステージ保存
        string currentScene = SceneManager.GetActiveScene().name;
        int currentStage = ExtractStageNumber(currentScene);
        int nextStage = currentStage + 1;

        PlayerPrefs.SetInt(NEXT_STAGE_KEY, nextStage);
        PlayerPrefs.Save();

        Time.timeScale = 0f;
    }

    // ★ネクストボタン用
    public void OnNextButton()
    {
        Time.timeScale = 1f;

        string currentScene = SceneManager.GetActiveScene().name;
        int currentStage = ExtractStageNumber(currentScene);

        if (currentStage >= maxStage)
        {
            SceneManager.LoadScene("LobbyScene");
        }
        else
        {
            SceneManager.LoadScene("Stage" + (currentStage + 1));
        }
    }

    int ExtractStageNumber(string sceneName)
    {
        Match match = Regex.Match(sceneName, @"\d+");
        if (match.Success)
        {
            return int.Parse(match.Value);
        }
        return 1;
    }

    void GameOver()
    {
        if (isGameOver) return; // すでにクリア/ゲームオーバー済みなら何もしない
        isGameOver = true;

        if (gameOverPanel != null)
        {
            gameOverPanel.SetActive(true);
        }

        // ★ゲームオーバー時はStage1に戻す
        PlayerPrefs.SetInt(NEXT_STAGE_KEY, 1);
        PlayerPrefs.Save();

        Time.timeScale = 0f;
    }

    void UpdateUI()
    {
        if (timeText != null)
        {
            timeText.text = $"Time: {currentTime:F2}";
        }

        if (keyText != null)
        {
            keyText.text = $"Keys: {collectedItems}/{totalItems}";
        }
    }

    void UpdateBestTimeUI()
    {
        if (bestTimeText == null) return;

        float bestTime = PlayerPrefs.GetFloat(GetBestTimeKey(), float.MaxValue);

        if (bestTime == float.MaxValue)
        {
            bestTimeText.text = "Best: ---";
        }
        else
        {
            bestTimeText.text = $"Best: {bestTime:F2}";
        }
    }

    void UpdateBestTime()
    {
        string bestTimeKey = GetBestTimeKey();
        float bestTime = PlayerPrefs.GetFloat(bestTimeKey, float.MaxValue);
        bool isNewRecord = currentTime < bestTime;

        if (isNewRecord)
        {
            PlayerPrefs.SetFloat(bestTimeKey, currentTime);

            if (newRecordText != null)
            {
                newRecordText.text = "NEW RECORD!";
                newRecordText.gameObject.SetActive(true);
            }
        }
        else if (newRecordText != null)
        {
            newRecordText.gameObject.SetActive(false);
        }

        UpdateBestTimeUI();
    }

    string GetBestTimeKey()
    {
        return BEST_TIME_KEY_PREFIX + SceneManager.GetActiveScene().name;
    }

    IEnumerator StartCountdown()
    {
        Time.timeScale = 0f;

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(true);
        }

        yield return ShowCount("3");
        yield return ShowCount("2");
        yield return ShowCount("1");
        yield return ShowCount("start!");

        if (countdownText != null)
        {
            countdownText.gameObject.SetActive(false);
        }

        Time.timeScale = 1f;
        isGameStarted = true;
    }

    IEnumerator ShowCount(string text)
    {
        countdownText.text = text;

        float timer = 0f;
        while (timer < 1f)
        {
            timer += Time.unscaledDeltaTime;
            yield return null;
        }
    }

    public void LoadLobbyScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene("LobbyScene");
    }

    public bool CanPause => isGameStarted && !isGameOver && !isPaused && Time.timeScale > 0f;
    public bool IsPaused => isPaused;

    public void PauseGame()
    {
        if (!CanPause) return;
        isPaused = true;
        Time.timeScale = 0f;
    }

    public void ResumeGame()
    {
        if (!isPaused || isGameOver) return;
        isPaused = false;
        Time.timeScale = 1f;
    }

    [ContextMenu("Build Game UI In Edit Mode")]
    private void BuildGameUIInEditMode()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning("Play中には実行しないでください。");
            return;
        }

        // CharacterStatusHUD
        if (statusHUD == null)
            statusHUD = FindFirstObjectByType<CharacterStatusHUD>(FindObjectsInactive.Include);

        if (statusHUD == null)
        {
            GameObject statusObject = new GameObject("StatusUI", typeof(RectTransform));
            GameObject mainCanvas = GameObject.Find("Canvas");
            statusObject.transform.SetParent(mainCanvas != null ? mainCanvas.transform : transform, false);
            statusHUD = statusObject.AddComponent<CharacterStatusHUD>();
        }

        statusHUD.BuildForEditor(statusFont);

        // MinimapController
        if (minimapController != null)
        {
            minimapController.BuildForEditor();
        }

        // PauseMenuController
        if (pauseMenu == null)
            pauseMenu = FindFirstObjectByType<PauseMenuController>(FindObjectsInactive.Include);

        if (pauseMenu == null)
        {
            GameObject pauseObject = new GameObject("PauseUI", typeof(RectTransform));
            GameObject mainCanvas = GameObject.Find("Canvas");
            pauseObject.transform.SetParent(mainCanvas != null ? mainCanvas.transform : transform, false);
            pauseMenu = pauseObject.AddComponent<PauseMenuController>();
        }

        pauseMenu.BuildForEditor(statusFont);

        Debug.Log("Game UIをEdit Modeで作成しました。Sceneを保存してください。");
    }
}
