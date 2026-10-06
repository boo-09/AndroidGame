using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("Menu appearance")]
    [SerializeField] private Sprite panelSprite;
    [SerializeField] private Sprite resumeSprite;
    [SerializeField] private Sprite exitSprite;
    [SerializeField] private Sprite menuButtonSprite;
    [SerializeField] private Material titleMaterial;
    [SerializeField] private Material menuLabelMaterial;

    private GameManager manager;
    private TMP_FontAsset font;

    private GameObject canvasObject;
    private GameObject overlay;

    private Button pauseButton;
    private Button resumeButton;
    private Button lobbyButton;

    private RectTransform safeArea;
    private RectTransform menuPanel;


    public void Initialize(
        GameManager gameManager,
        TMP_FontAsset japaneseFont)
    {
        manager = gameManager;
        font = japaneseFont;

        EnsureHierarchy();
        WireButtons();

        overlay.SetActive(false);
        pauseButton.gameObject.SetActive(false);
    }


    public void BuildForEditor(
        TMP_FontAsset japaneseFont)
    {
        if (Application.isPlaying)
            return;

        font = japaneseFont;

        EnsureHierarchy();

        if (overlay != null)
        {
            overlay.SetActive(false);
        }

        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(false);
        }

        Debug.Log("PauseUIを作成しました。");
    }


    private void Update()
    {
        if (manager == null ||
            canvasObject == null ||
            safeArea == null)
        {
            return;
        }

        Rect safe =
            Screen.safeArea;

        safeArea.anchorMin =
            new Vector2(
                safe.xMin / Screen.width,
                safe.yMin / Screen.height
            );

        safeArea.anchorMax =
            new Vector2(
                safe.xMax / Screen.width,
                safe.yMax / Screen.height
            );

        UpdateMenuLayout();

        if (Keyboard.current != null &&
            Keyboard.current.escapeKey
                .wasPressedThisFrame)
        {
            if (manager.IsPaused)
            {
                Resume();
            }
            else if (manager.CanPause)
            {
                Pause();
            }
        }


        if (pauseButton != null)
        {
            pauseButton.gameObject.SetActive(
                manager.CanPause
            );
        }
    }


    private void EnsureHierarchy()
    {
        // =================================
        // PauseUI
        // =================================

        canvasObject = gameObject;
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = 500;
        canvas.overrideSorting = true;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
        }
        if (GetComponent<GraphicRaycaster>() == null)
            gameObject.AddComponent<GraphicRaycaster>();
        if (GetComponent<NestedCanvasLayout>() == null)
            gameObject.AddComponent<NestedCanvasLayout>();


        // =================================
        // SafeArea
        // =================================

        safeArea =
            FindOrCreateRect(
                "SafeArea",
                canvasObject.transform
            );

        if (safeArea.anchorMin ==
                safeArea.anchorMax)
        {
            safeArea.anchorMin =
                Vector2.zero;

            safeArea.anchorMax =
                Vector2.one;

            safeArea.offsetMin =
                Vector2.zero;

            safeArea.offsetMax =
                Vector2.zero;
        }


        // =================================
        // PauseButton
        // =================================

        pauseButton =
            FindOrCreateButton(
                "PauseButton",
                safeArea,
                "Menu",
                new Color(
                    0.09f,
                    0.14f,
                    0.19f,
                    0.92f
                ),
                new Vector2(
                    160f,
                    56f
                ),
                22f
            );

        RectTransform pauseRect =
            pauseButton.GetComponent<RectTransform>();

        if (pauseRect.anchoredPosition ==
            Vector2.zero)
        {
            pauseRect.anchorMin =
                Vector2.one;

            pauseRect.anchorMax =
                Vector2.one;

            pauseRect.pivot =
                Vector2.one;

            pauseRect.anchoredPosition =
                new Vector2(
                    -24f,
                    -16f
                );
        }


        // =================================
        // Menu button appearance (keep the HUD rectangle above the timer).
        Image menuImage = pauseButton.GetComponent<Image>();
        if (menuButtonSprite != null)
        {
            menuImage.sprite = menuButtonSprite;
            menuImage.color = Color.white;
            menuImage.type = Image.Type.Simple;
            menuImage.preserveAspect = false;
        }
        TextMeshProUGUI menuLabel = pauseButton.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        menuLabel.text = "Menu";
        menuLabel.color = Color.white;
        menuLabel.fontStyle = FontStyles.Bold;
        menuLabel.fontWeight = FontWeight.Bold;
        menuLabel.enableAutoSizing = false;
        menuLabel.fontSize = menuLabel.fontSizeMin = menuLabel.fontSizeMax = 30f;
        menuLabel.alignment = TextAlignmentOptions.MidlineGeoAligned;
        menuLabel.textWrappingMode = TextWrappingModes.NoWrap;
        menuLabel.margin = new Vector4(8f, 0f, 8f, 0f);
        RectTransform menuLabelRect = menuLabel.rectTransform;
        menuLabelRect.anchorMin = Vector2.zero;
        menuLabelRect.anchorMax = Vector2.one;
        menuLabelRect.pivot = new Vector2(0.5f, 0.5f);
        menuLabelRect.offsetMin = menuLabelRect.offsetMax = Vector2.zero;
        if (menuLabelMaterial != null) menuLabel.fontSharedMaterial = menuLabelMaterial;
        else if (titleMaterial != null) menuLabel.fontSharedMaterial = titleMaterial;

        // PauseOverlay
        // =================================

        Transform existingOverlay =
            canvasObject.transform
                .Find("PauseOverlay");

        if (existingOverlay == null)
        {
            overlay =
                new GameObject(
                    "PauseOverlay",
                    typeof(RectTransform),
                    typeof(Image)
                );

            RectTransform overlayRect =
                overlay
                    .GetComponent<RectTransform>();

            overlayRect.SetParent(
                canvasObject.transform,
                false
            );

            overlayRect.anchorMin =
                Vector2.zero;

            overlayRect.anchorMax =
                Vector2.one;

            overlayRect.offsetMin =
                Vector2.zero;

            overlayRect.offsetMax =
                Vector2.zero;

            Image dimmer =
                overlay.GetComponent<Image>();

            dimmer.color =
                new Color(
                    0.015f,
                    0.025f,
                    0.04f,
                    0.76f
                );
        }
        else
        {
            overlay =
                existingOverlay.gameObject;
        }


        RectTransform overlayParent =
            overlay.GetComponent<RectTransform>();


        // =================================
        // PausePanel
        // =================================

        RectTransform card = FindOrCreateRect("PausePanel", overlayParent);
        TextMeshProUGUI title = FindOrCreateText("Title", card, "Pause", 64f);

        resumeButton = FindOrCreateButton(
            "ResumeButton", card, "Resume", Color.white, new Vector2(336f, 141f), 25f);
        lobbyButton = FindOrCreateButton(
            "LobbyButton", card, "Exit", Color.white, new Vector2(336f, 141f), 25f);

        ApplyMenuAppearance(card, title);
    }

    private void ApplyMenuAppearance(RectTransform card, TextMeshProUGUI title)
    {
        menuPanel = card;
        PlaceCentered(card, Vector2.zero, new Vector2(500f, 460f));

        Image background = card.GetComponent<Image>();
        if (background == null) background = card.gameObject.AddComponent<Image>();
        background.sprite = panelSprite;
        background.color = new Color(0.14f, 0.075f, 0.27f, 0.98f);
        background.type = Image.Type.Simple;

        title.text = "Pause";
        title.color = new Color(1f, 0.82f, 0.06f, 1f);
        title.fontSize = title.fontSizeMax = 64f;
        title.fontSizeMin = 40f;
        title.fontStyle = FontStyles.Bold;
        if (titleMaterial != null) title.fontSharedMaterial = titleMaterial;
        PlaceCentered(title.rectTransform, new Vector2(0f, 175f), new Vector2(400f, 90f));

        ApplyMenuButton(resumeButton, resumeSprite, "Resume", new Vector2(0f, 40f));
        ApplyMenuButton(lobbyButton, exitSprite, "Exit", new Vector2(0f, -125f));
        UpdateMenuLayout();
    }

    private static void ApplyMenuButton(Button button, Sprite sprite, string caption, Vector2 position)
    {
        PlaceCentered(button.GetComponent<RectTransform>(), position, new Vector2(336f, 141f));
        Image image = button.GetComponent<Image>();
        image.sprite = sprite;
        image.color = sprite != null ? Color.white : new Color(0.52f, 0.26f, 0.85f, 1f);
        image.type = Image.Type.Simple;
        image.preserveAspect = true;

        TextMeshProUGUI label = button.transform.Find("Label").GetComponent<TextMeshProUGUI>();
        label.text = caption;
        // These assets include their captions; keep the text as a fallback.
        label.enabled = sprite == null;
    }

    private void UpdateMenuLayout()
    {
        if (menuPanel == null || Screen.width <= 0 || Screen.height <= 0) return;

        Canvas canvas = GetComponent<Canvas>();
        NestedCanvasLayout nestedLayout = GetComponent<NestedCanvasLayout>();
        float scale = nestedLayout != null && !canvas.isRootCanvas
            ? nestedLayout.LayoutScaleFactor : canvas.scaleFactor;
        if (scale <= 0f) return;

        Rect safe = Screen.safeArea;
        float fit = Mathf.Clamp01(Mathf.Min(safe.width / (548f * scale), safe.height / (508f * scale)));
        menuPanel.localScale = Vector3.one * fit;
        menuPanel.anchoredPosition = (safe.center - new Vector2(Screen.width, Screen.height) * 0.5f) / scale;
    }


    private void WireButtons()
    {
        if (pauseButton != null)
        {
            pauseButton.onClick
                .RemoveListener(Pause);

            pauseButton.onClick
                .AddListener(Pause);
        }

        if (resumeButton != null)
        {
            resumeButton.onClick
                .RemoveListener(Resume);

            resumeButton.onClick
                .AddListener(Resume);
        }

        if (lobbyButton != null)
        {
            lobbyButton.onClick
                .RemoveListener(ReturnToLobby);

            lobbyButton.onClick
                .AddListener(ReturnToLobby);
        }
    }


    private void Pause()
    {
        if (manager == null ||
            !manager.CanPause)
        {
            return;
        }

        manager.PauseGame();

        if (overlay != null)
        {
            overlay.SetActive(true);
        }
    }


    private void Resume()
    {
        if (manager == null ||
            !manager.IsPaused)
        {
            return;
        }

        if (overlay != null)
        {
            overlay.SetActive(false);
        }

        manager.ResumeGame();
    }


    private void ReturnToLobby()
    {
        if (manager == null ||
            !manager.IsPaused)
        {
            return;
        }

        manager.LoadLobbyScene();
    }


    private RectTransform FindOrCreateRect(
        string name,
        Transform parent)
    {
        Transform existing =
            parent.Find(name);

        if (existing != null)
        {
            return existing
                as RectTransform;
        }

        RectTransform rect =
            new GameObject(
                name,
                typeof(RectTransform)
            ).GetComponent<RectTransform>();

        rect.SetParent(
            parent,
            false
        );

        return rect;
    }


    private Button FindOrCreateButton(
        string name,
        Transform parent,
        string caption,
        Color color,
        Vector2 size,
        float textSize)
    {
        Transform existing =
            parent.Find(name);

        if (existing != null)
        {
            Button existingButton =
                existing.GetComponent<Button>();

            if (existingButton != null)
            {
                return existingButton;
            }
        }


        RectTransform rect =
            FindOrCreateRect(
                name,
                parent
            );

        rect.sizeDelta = size;


        Image image =
            rect.GetComponent<Image>();

        if (image == null)
        {
            image =
                rect.gameObject
                    .AddComponent<Image>();
        }

        image.color = color;


        Button button =
            rect.GetComponent<Button>();

        if (button == null)
        {
            button =
                rect.gameObject
                    .AddComponent<Button>();
        }

        button.targetGraphic =
            image;


        TextMeshProUGUI label =
            FindOrCreateText(
                "Label",
                rect,
                caption,
                textSize
            );

        label.rectTransform.anchorMin =
            Vector2.zero;

        label.rectTransform.anchorMax =
            Vector2.one;

        label.rectTransform.offsetMin =
            Vector2.zero;

        label.rectTransform.offsetMax =
            Vector2.zero;

        return button;
    }


    private TextMeshProUGUI FindOrCreateText(
        string name,
        Transform parent,
        string content,
        float size)
    {
        Transform existing =
            parent.Find(name);

        if (existing != null)
        {
            TextMeshProUGUI existingText =
                existing
                    .GetComponent<TextMeshProUGUI>();

            if (existingText != null)
            {
                return existingText;
            }
        }


        RectTransform rect =
            FindOrCreateRect(
                name,
                parent
            );

        TextMeshProUGUI label =
            rect.GetComponent<TextMeshProUGUI>();

        if (label == null)
        {
            label =
                rect.gameObject
                    .AddComponent<TextMeshProUGUI>();
        }


        if (font != null)
        {
            label.font = font;
        }

        label.text = content;
        label.fontSize = size;
        label.fontStyle = FontStyles.Bold;

        label.alignment =
            TextAlignmentOptions.Center;

        label.color = Color.white;

        label.enableAutoSizing = true;
        label.fontSizeMin = 16f;
        label.fontSizeMax = size;

        label.raycastTarget = false;

        return label;
    }


    private static void PlaceCentered(
        RectTransform rect,
        Vector2 position,
        Vector2 size)
    {
        rect.anchorMin =
            new Vector2(0.5f, 0.5f);

        rect.anchorMax =
            new Vector2(0.5f, 0.5f);

        rect.pivot =
            new Vector2(0.5f, 0.5f);

        rect.anchoredPosition =
            position;

        rect.sizeDelta = size;
    }
}
