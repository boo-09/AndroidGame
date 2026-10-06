using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Text.RegularExpressions;

public class CharacterStatusHUD : MonoBehaviour
{
    [Header("Layout (1080px reference width)")]
    [SerializeField] private Vector2 topLeftOffset = new Vector2(24, 18);
    [SerializeField] private float panelWidth = 600f;

    [Header("Theme")]
    [SerializeField] private Color labelColor =
        new Color(0.88f, 0.94f, 1f, 1f);

    [SerializeField] private Color valueColor = Color.white;

    [Header("Labels")]
    [SerializeField] private string floorLabel = "階層";
    [SerializeField] private string attackLabel = "攻撃力";
    [SerializeField] private string speedLabel = "移動速度";
    [SerializeField] private string defeatsLabel = "討伐数";

    [SerializeField] private TMP_FontAsset font;

    private PlayerController2 player;

    private RectTransform safeArea;
    private RectTransform statusPanel;

    public RectTransform MapParent { get; private set; }

    private GameObject hudRoot;

    private TextMeshProUGUI attackValue;
    private TextMeshProUGUI speedValue;
    private TextMeshProUGUI defeatsValue;

    private float previousAttack = float.NaN;
    private float previousSpeed = float.NaN;
    private int previousDefeats = -1;


    public void Initialize(
        PlayerController2 source,
        TMP_FontAsset japaneseFont)
    {
        player = source;

        if (font == null)
        {
            font = japaneseFont;
        }

        if (hudRoot == null)
            EnsureHierarchy();

        Refresh();
    }


    // Edit Mode用
    public void BuildForEditor(
        TMP_FontAsset japaneseFont)
    {
        if (Application.isPlaying)
            return;

        if (font == null)
        {
            font = japaneseFont;
        }

        EnsureHierarchy();


        Debug.Log("StatusUIを作成しました。");
    }


    private void LateUpdate()
    {
        if (hudRoot != null)
        {
            Refresh();
        }
    }


    private void EnsureHierarchy()
    {
        // =========================
        // StatusUI
        // =========================

        hudRoot = gameObject;
        Canvas canvas = GetComponent<Canvas>();
        if (canvas == null)
        {
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        }
        canvas.sortingOrder = 10;
        canvas.overrideSorting = true;

        CanvasScaler scaler = GetComponent<CanvasScaler>();
        if (scaler == null)
        {
            scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode =
                CanvasScaler.ScaleMode.ScaleWithScreenSize;

            scaler.referenceResolution =
                new Vector2(1080, 1920);

            scaler.matchWidthOrHeight = 0f;
        }
        if (GetComponent<NestedCanvasLayout>() == null)
            gameObject.AddComponent<NestedCanvasLayout>();


        // =========================
        // SafeArea
        // =========================

        safeArea = FindOrCreateRect(
            "SafeArea",
            hudRoot.transform,
            Vector2.zero,
            Vector2.zero
        );

        MapParent = safeArea;


        // =========================
        // StatusPanel
        // =========================

        Vector2 offset = new Vector2(
            220f,
            Mathf.Clamp(topLeftOffset.y, 0, 48)
        );

        float width =
            Mathf.Clamp(panelWidth, 480f, 600f);

        statusPanel = FindOrCreateRect(
            "StatusPanel",
            safeArea,
            offset,
            new Vector2(width, 56)
        );

        float cellWidth = width / 4f;


        TextMeshProUGUI floorValue =
            CreateOrFindCell(
                statusPanel,
                floorLabel,
                0,
                cellWidth
            );

        string digits =
            Regex.Match(
                SceneManager.GetActiveScene().name,
                @"\d+"
            ).Value;

        floorValue.text =
            (
                int.TryParse(digits, out int stage)
                    ? stage
                    : 1
            ).ToString(CultureInfo.InvariantCulture);


        attackValue =
            CreateOrFindCell(
                statusPanel,
                attackLabel,
                1,
                cellWidth
            );

        speedValue =
            CreateOrFindCell(
                statusPanel,
                speedLabel,
                2,
                cellWidth
            );

        defeatsValue =
            CreateOrFindCell(
                statusPanel,
                defeatsLabel,
                3,
                cellWidth
            );


    }


    private TextMeshProUGUI CreateOrFindCell(
        RectTransform parent,
        string label,
        int index,
        float width)
    {
        float x = index * width;

        // Label
        Transform labelTransform =
            parent.Find(label + "Label");

        if (labelTransform == null)
        {
            CreateText(
                parent,
                label + "Label",
                label,
                new Vector2(x + 8, 6),
                new Vector2(width - 76, 44),
                18,
                labelColor,
                false
            );
        }


        // Value
        Transform valueTransform =
            parent.Find(label + "Value");

        if (valueTransform != null)
        {
            return valueTransform
                .GetComponent<TextMeshProUGUI>();
        }

        return CreateText(
            parent,
            label + "Value",
            "0",
            new Vector2(x + width - 63, 6),
            new Vector2(52, 44),
            21,
            valueColor,
            true
        );
    }


    private TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string content,
        Vector2 position,
        Vector2 size,
        float fontSize,
        Color color,
        bool numeric)
    {
        RectTransform rect =
            CreateRect(
                name,
                parent,
                position,
                size
            );

        TextMeshProUGUI text =
            rect.gameObject
                .AddComponent<TextMeshProUGUI>();

        if (font != null)
        {
            text.font = font;
        }

        text.text = content;
        text.fontSize = fontSize;

        text.fontStyle =
            numeric
                ? FontStyles.Bold
                : FontStyles.Normal;

        text.color = color;

        text.outlineColor =
            new Color32(8, 15, 22, 255);

        text.outlineWidth = 0.15f;

        text.alignment =
            numeric
                ? TextAlignmentOptions.MidlineRight
                : TextAlignmentOptions.MidlineLeft;

        text.textWrappingMode =
            TextWrappingModes.NoWrap;

        text.overflowMode =
            TextOverflowModes.Ellipsis;

        text.enableAutoSizing = true;
        text.fontSizeMin = 12;
        text.fontSizeMax = fontSize;

        text.raycastTarget = false;

        return text;
    }


    private RectTransform FindOrCreateRect(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        Transform existing = parent.Find(name);

        if (existing != null)
        {
            return existing as RectTransform;
        }

        return CreateRect(
            name,
            parent,
            position,
            size
        );
    }


    private static RectTransform CreateRect(
        string name,
        Transform parent,
        Vector2 position,
        Vector2 size)
    {
        RectTransform rect =
            new GameObject(
                name,
                typeof(RectTransform)
            ).GetComponent<RectTransform>();

        rect.SetParent(parent, false);

        rect.anchorMin =
            new Vector2(0, 1);

        rect.anchorMax =
            new Vector2(0, 1);

        rect.pivot =
            new Vector2(0, 1);

        rect.anchoredPosition =
            new Vector2(
                position.x,
                -position.y
            );

        rect.sizeDelta = size;

        return rect;
    }


    private void Refresh()
    {
        if (safeArea == null ||
            statusPanel == null ||
            hudRoot == null)
        {
            return;
        }

        Rect safe = Screen.safeArea;

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

        safeArea.offsetMin = Vector2.zero;
        safeArea.offsetMax = Vector2.zero;

        Canvas canvas =
            hudRoot.GetComponent<Canvas>();

        if (canvas == null)
            return;

        NestedCanvasLayout nestedLayout = canvas.GetComponent<NestedCanvasLayout>();
        float canvasScale = nestedLayout != null && !canvas.isRootCanvas
            ? nestedLayout.LayoutScaleFactor
            : canvas.scaleFactor;

        float safeWidth =
            safe.width /
            Mathf.Max(0.01f, canvasScale);

        float mapSize =
            Mathf.Min(
                160f,
                safeWidth * 0.15f
            );

        float statusLeft =
            24f +
            mapSize +
            12f +
            Mathf.Clamp(
                topLeftOffset.x,
                0,
                48
            );

        statusPanel.anchoredPosition =
            new Vector2(
                statusLeft,
                -Mathf.Clamp(
                    topLeftOffset.y,
                    0,
                    48
                )
            );

        float availableStatusWidth =
            Mathf.Max(
                1f,
                safeWidth -
                224f -
                16f -
                statusLeft
            );

        float statusScale =
            Mathf.Min(
                1f,
                availableStatusWidth /
                statusPanel.rect.width
            );

        statusPanel.localScale =
            Vector3.one * statusScale;


        if (player == null)
            return;


        if (attackValue != null &&
            previousAttack != player.attackDamage)
        {
            previousAttack =
                player.attackDamage;

            attackValue.text =
                previousAttack.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture
                );
        }


        if (speedValue != null &&
            previousSpeed != player.speed)
        {
            previousSpeed =
                player.speed;

            speedValue.text =
                previousSpeed.ToString(
                    "0.##",
                    CultureInfo.InvariantCulture
                );
        }


        if (defeatsValue != null &&
            previousDefeats != player.EnemiesDefeated)
        {
            previousDefeats =
                player.EnemiesDefeated;

            defeatsValue.text =
                previousDefeats.ToString(
                    CultureInfo.InvariantCulture
                );
        }
    }


    private void OnDisable()
    {
        if (Application.isPlaying &&
            hudRoot != null)
        {
            hudRoot.SetActive(false);
        }
    }


    private void OnEnable()
    {
        if (Application.isPlaying &&
            hudRoot != null)
        {
            hudRoot.SetActive(true);
        }
    }
}
