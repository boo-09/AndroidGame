using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>全ステージ共通のBGM・効果音。Resources/GameAudioから自動生成する。</summary>
public sealed class GameAudio : MonoBehaviour
{
    [SerializeField, Tooltip("キャラクター側はON。音声設定はResources/GameAudioで共通管理する。")]
    private bool animationEventReceiver;
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource soundSource;
    [SerializeField] private AudioClip dungeonMusic;
    [SerializeField] private AudioClip gameOverSound;
    [SerializeField] private AudioClip gameClearSound;
    [SerializeField] private AudioClip enemyDefeatedSound;
    [SerializeField] private AudioClip swordSwingClip;
    [SerializeField, Range(0f, 1f)] private float swordSwingVolume = 1f;
    [SerializeField, Min(0f), Tooltip("敵の撃破から効果音を鳴らすまでの秒数")]
    private float enemyDefeatedDelay = 0.3f;

    private static GameAudio instance;
    private bool isDungeon;
    private bool resultPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetInstance()
    {
        instance = null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize()
    {
        if (instance != null) return;

        var prefab = Resources.Load<GameAudio>("GameAudio");
        if (prefab != null)
            Instantiate(prefab);
        else
            Debug.LogError("Resources/GameAudio prefab is missing.");
    }

    private void Awake()
    {
        // Animatorと同じオブジェクトではイベント受信だけを担当する。
        if (animationEventReceiver) return;

        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        soundSource.playOnAwake = false;
        soundSource.spatialBlend = 0f;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDestroy()
    {
        if (animationEventReceiver) return;

        SceneManager.sceneLoaded -= OnSceneLoaded;
        if (instance == this) instance = null;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive) return;

        StopAllCoroutines();
        int stageNumber;
        isDungeon = scene.name.StartsWith("Stage", System.StringComparison.Ordinal)
            && int.TryParse(scene.name.Substring(5), out stageNumber) && stageNumber > 0;
        resultPlaying = false;
        soundSource.Stop();
        musicSource.Stop();

        if (isDungeon && dungeonMusic != null)
        {
            musicSource.clip = dungeonMusic;
            musicSource.Play();
        }
    }

    public static void PlayGameOver()
    {
        if (instance != null) instance.PlayResult(instance.gameOverSound);
    }

    public static void PlayGameClear()
    {
        if (instance != null) instance.PlayResult(instance.gameClearSound);
    }

    private void PlayResult(AudioClip clip)
    {
        if (!isDungeon || resultPlaying) return;

        resultPlaying = true;
        StopAllCoroutines();
        musicSource.Stop();
        soundSource.Stop();
        if (clip != null) soundSource.PlayOneShot(clip);
    }

    // 既存のAnimation Event名を保ち、共通のAudioSourceから鳴らす。
    public void PlaySwordSwingSE()
    {
        if (instance != null && instance.isDungeon && !instance.resultPlaying
            && instance.swordSwingClip != null)
            instance.soundSource.PlayOneShot(instance.swordSwingClip, instance.swordSwingVolume);
    }

    public static void PlayEnemyDefeated()
    {
        if (instance != null && instance.isDungeon && !instance.resultPlaying
            && instance.enemyDefeatedSound != null)
            instance.StartCoroutine(instance.PlayEnemyDefeatedAfterDelay());
    }

    private IEnumerator PlayEnemyDefeatedAfterDelay()
    {
        // 敵が消えた後も共通オブジェクトで待ち、時間停止中も再生できるようにする。
        if (enemyDefeatedDelay > 0f)
            yield return new WaitForSecondsRealtime(enemyDefeatedDelay);

        if (isDungeon && !resultPlaying && enemyDefeatedSound != null)
            soundSource.PlayOneShot(enemyDefeatedSound);
    }
}
