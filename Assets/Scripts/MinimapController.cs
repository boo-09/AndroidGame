using UnityEngine;
using UnityEngine.UI;

public class MinimapController : MonoBehaviour
{
    [SerializeField] private float cameraHeight = 30f;
    [SerializeField] private float mapPadding = 2f;
    [SerializeField] private float revealRadius = 4f;
    public float RevealRadius => revealRadius;

    public void AddRevealRadius(float amount)
    {
        revealRadius = Mathf.Max(0.1f, revealRadius + amount);
        UpdateExploration();
    }
    [SerializeField] private Vector2 displaySize = new Vector2(160f, 160f);
    [SerializeField] private Vector2 screenMargin = new Vector2(24f, 24f);

    private Transform target;
    private Camera minimapCamera;
    private RenderTexture minimapTexture;
    private RectTransform playerMarker;
    private Texture2D explorationTexture;
    private Color32[] explorationPixels;
    private const int ExplorationResolution = 128;
    private RectTransform borderRect;
    private RectTransform mapRect;
    private RectTransform attackButton;
    private RectTransform moveJoystick;
    private RawImage mapImage;
    private RawImage fogImage;
    private readonly Vector3[] touchCorners = new Vector3[4];
    private Vector2 requestedDisplaySize;
    private const float MapTop = 18f;
    private const float TouchGap = 16f;
    public void Initialize(Transform followTarget)
    {
        target = followTarget;
        requestedDisplaySize = displaySize;

        EnsureHierarchy();

        CreateRuntimeTextures();

        if (minimapCamera != null)
        {
            minimapCamera.enabled = true;

            Camera mainCamera = Camera.main;

            if(mainCamera != null)
            {
                minimapCamera.cullingMask = mainCamera.cullingMask;
            }
        }

        FrameStage();
        UpdateMapSize();
        UpdateExploration();
    }
    public void BuildForEditor()
{
    if (Application.isPlaying)
        return;

    requestedDisplaySize = displaySize;

    EnsureHierarchy();

    // Edit Modeでは
    // MinimapCameraがGameViewを描画しないようにする
    if (minimapCamera != null)
    {
        minimapCamera.enabled = false;
    }

    Debug.Log("Minimap UIを作成しました。");
}
    private void LateUpdate()
    {
        UpdateMapSize();
        UpdateExploration();
    }

    private void EnsureHierarchy()
{
    // =====================================
    // MinimapCamera
    // =====================================

    Transform cameraTransform =
        transform.Find("MinimapCamera");

    if (cameraTransform == null)
    {
        GameObject cameraObject =
            new GameObject("MinimapCamera");

        cameraObject.transform
            .SetParent(transform, false);

        minimapCamera =
            cameraObject.AddComponent<Camera>();

        minimapCamera.orthographic = true;
        minimapCamera.aspect = 1f;

        minimapCamera.clearFlags =
            CameraClearFlags.SolidColor;

        minimapCamera.backgroundColor =
            new Color(
                0.08f,
                0.08f,
                0.08f,
                1f
            );

        minimapCamera.depth = -10f;
    }
    else
    {
        minimapCamera =
            cameraTransform.GetComponent<Camera>();

        if (minimapCamera == null)
        {
            minimapCamera =
                cameraTransform.gameObject
                    .AddComponent<Camera>();
        }
    }


    // =====================================
    // UI親
    // =====================================

    Transform uiParent;
    {
        Transform existingCanvas =
            transform.Find("MinimapCanvas");

        GameObject canvasObject;

        if (existingCanvas == null)
        {
            canvasObject =
                new GameObject(
                    "MinimapCanvas",
                    typeof(RectTransform),
                    typeof(Canvas),
                    typeof(CanvasScaler),
                    typeof(GraphicRaycaster)
                );

            canvasObject.transform
                .SetParent(transform, false);

            Canvas canvas =
                canvasObject.GetComponent<Canvas>();

            canvas.renderMode =
                RenderMode.ScreenSpaceOverlay;

            canvas.sortingOrder = 10;

            CanvasScaler scaler =
                canvasObject
                    .GetComponent<CanvasScaler>();

            scaler.uiScaleMode =
                CanvasScaler.ScaleMode
                    .ScaleWithScreenSize;

            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0f;
        }
        else
        {
            canvasObject =
                existingCanvas.gameObject;
        }

        uiParent = canvasObject.transform;
    }


    // =====================================
    // MinimapBorder
    // =====================================

    Transform borderTransform =
        uiParent.Find("MinimapBorder");

    if (borderTransform == null)
    {
        GameObject borderObject =
            new GameObject(
                "MinimapBorder",
                typeof(RectTransform),
                typeof(Image)
            );

        borderObject.transform
            .SetParent(uiParent, false);

        Image border =
            borderObject.GetComponent<Image>();

        border.color =
            new Color(
                0f,
                0f,
                0f,
                0.8f
            );

        border.raycastTarget = false;

        borderRect =
            borderObject
                .GetComponent<RectTransform>();

        borderRect.anchorMin = borderRect.anchorMax = borderRect.pivot = new Vector2(0f, 1f);
        borderRect.anchoredPosition = new Vector2(screenMargin.x, -MapTop);

        borderRect.sizeDelta =
            displaySize +
            new Vector2(12f, 12f);
    }
    else
    {
        borderRect =
            borderTransform as RectTransform;
    }


    // =====================================
    // MinimapImage
    // =====================================

    Transform mapTransform =
        borderRect.Find("MinimapImage");

    if (mapTransform == null)
    {
        GameObject mapObject =
            new GameObject(
                "MinimapImage",
                typeof(RectTransform),
                typeof(RawImage)
            );

        mapObject.transform.SetParent(
            borderRect,
            false
        );

        mapImage =
            mapObject.GetComponent<RawImage>();

        mapImage.raycastTarget = false;

        mapRect =
            mapImage.rectTransform;

        mapRect.anchorMin =
            new Vector2(0.5f, 0.5f);

        mapRect.anchorMax =
            new Vector2(0.5f, 0.5f);

        mapRect.pivot =
            new Vector2(0.5f, 0.5f);

        mapRect.anchoredPosition =
            Vector2.zero;

        mapRect.sizeDelta =
            displaySize;
    }
    else
    {
        mapImage =
            mapTransform
                .GetComponent<RawImage>();

        mapRect =
            mapTransform as RectTransform;
    }


    // =====================================
    // UnexploredMap
    // =====================================

    Transform fogTransform =
        mapRect.Find("UnexploredMap");

    if (fogTransform == null)
    {
        GameObject fogObject =
            new GameObject(
                "UnexploredMap",
                typeof(RectTransform),
                typeof(RawImage)
            );

        fogObject.transform
            .SetParent(mapRect, false);

        fogImage =
            fogObject.GetComponent<RawImage>();

        fogImage.raycastTarget = false;

        RectTransform fogRect =
            fogImage.rectTransform;

        fogRect.anchorMin = Vector2.zero;
        fogRect.anchorMax = Vector2.one;
        fogRect.offsetMin = Vector2.zero;
        fogRect.offsetMax = Vector2.zero;
    }
    else
    {
        fogImage =
            fogTransform
                .GetComponent<RawImage>();
    }


    // =====================================
    // PlayerMarker
    // =====================================

    Transform markerTransform =
        borderRect.Find("PlayerMarker");

    if (markerTransform == null)
    {
        GameObject markerObject =
            new GameObject(
                "PlayerMarker",
                typeof(RectTransform),
                typeof(Image)
            );

        markerObject.transform
            .SetParent(borderRect, false);

        Image marker =
            markerObject.GetComponent<Image>();

        marker.color =
            new Color(
                1f,
                0.2f,
                0.1f,
                1f
            );

        marker.raycastTarget = false;

        playerMarker =
            marker.rectTransform;

        playerMarker.anchorMin =
            new Vector2(0.5f, 0.5f);

        playerMarker.anchorMax =
            new Vector2(0.5f, 0.5f);

        playerMarker.pivot =
            new Vector2(0.5f, 0.5f);

        playerMarker.anchoredPosition =
            Vector2.zero;

        playerMarker.sizeDelta =
            new Vector2(14f, 14f);

        playerMarker.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                45f
            );
    }
    else
    {
        playerMarker =
            markerTransform as RectTransform;
    }


    GameObject button = GameObject.Find("AttackButton");
    if (button != null) attackButton = button.GetComponent<RectTransform>();
}
    private void CreateRuntimeTextures()
{
    if (minimapTexture == null)
    {
        minimapTexture =
            new RenderTexture(
                512,
                512,
                16
            );

        minimapTexture.name =
            "MinimapRenderTexture";

        minimapTexture.Create();
    }

    if (minimapCamera != null)
    {
        minimapCamera.targetTexture =
            minimapTexture;
    }

    if (mapImage != null)
    {
        mapImage.texture =
            minimapTexture;
    }


    if (explorationTexture == null)
    {
        explorationTexture =
            new Texture2D(
                ExplorationResolution,
                ExplorationResolution,
                TextureFormat.RGBA32,
                false
            );

        explorationTexture.name =
            "MinimapExploration";

        explorationTexture.wrapMode =
            TextureWrapMode.Clamp;

        explorationTexture.filterMode =
            FilterMode.Bilinear;


        explorationPixels =
            new Color32[
                ExplorationResolution *
                ExplorationResolution
            ];

        for (
            int i = 0;
            i < explorationPixels.Length;
            i++)
        {
            explorationPixels[i] =
                new Color32(
                    12,
                    12,
                    12,
                    255
                );
        }

        explorationTexture.SetPixels32(
            explorationPixels
        );

        explorationTexture.Apply(false);
    }

    if (fogImage != null)
    {
        fogImage.texture =
            explorationTexture;
    }
}

    private void UpdateMapSize()
    {
        if (borderRect == null || mapRect == null) return;

        Canvas canvas = borderRect.GetComponentInParent<Canvas>();
        if (canvas == null) return;
        float canvasScale = canvas.scaleFactor;
        float safeWidth = Screen.safeArea.width / Mathf.Max(0.01f, canvasScale);
        borderRect.anchoredPosition = new Vector2(
            screenMargin.x + Screen.safeArea.xMin / Mathf.Max(0.01f, canvasScale),
            -MapTop - (Screen.height - Screen.safeArea.yMax) / Mathf.Max(0.01f, canvasScale));
        float size = Mathf.Min(160f, requestedDisplaySize.x, requestedDisplaySize.y, safeWidth * 0.15f);
        if (attackButton == null)
        {
            GameObject button = GameObject.Find("AttackButton");
            if (button != null) attackButton = button.GetComponent<RectTransform>();
        }
        if (moveJoystick == null)
        {
            GameObject joystick = GameObject.Find("UI_Virtual_Joystick_Move");
            if (joystick != null) moveJoystick = joystick.GetComponent<RectTransform>();
        }
        float buttonTop = Mathf.Max(GetTouchTop(attackButton), GetTouchTop(moveJoystick));
        if (!float.IsNegativeInfinity(buttonTop))
        {
            float freeHeight = (Screen.safeArea.yMax - buttonTop) / Mathf.Max(0.01f, canvasScale);
            size = Mathf.Min(size, freeHeight - MapTop - TouchGap - 12f);
        }

        bool hasRoom = size >= 72f;
        borderRect.gameObject.SetActive(hasRoom);
        if (!hasRoom) return;
        Vector2 square = Vector2.one * size;
        mapRect.sizeDelta = square;
        borderRect.sizeDelta = square + Vector2.one * 12f;
        displaySize = square;
    }

    private float GetTouchTop(RectTransform touchArea)
    {
        if (touchArea == null) return float.NegativeInfinity;
        touchArea.GetWorldCorners(touchCorners);
        float top = float.NegativeInfinity;
        foreach (Vector3 corner in touchCorners)
        {
            top = Mathf.Max(top, RectTransformUtility.WorldToScreenPoint(null, corner).y);
        }
        return top;
    }

    private void FrameStage()
    {
        if(target == null || minimapCamera == null)
            return;

        Bounds bounds = new Bounds(target.position, Vector3.zero);
        foreach (MeshRenderer renderer in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None))
        {
            if (!renderer.enabled || renderer.gameObject.scene != gameObject.scene ||
                renderer.GetComponentInParent<Canvas>() != null ||
                renderer.GetComponentInParent<PlayerController2>() != null ||
                renderer.GetComponentInParent<Enemy>() != null ||
                (minimapCamera.cullingMask & (1 << renderer.gameObject.layer)) == 0)
            {
                continue;
            }

            bounds.Encapsulate(renderer.bounds);
        }

        minimapCamera.orthographicSize = Mathf.Max(bounds.extents.x, bounds.extents.z) +
            Mathf.Max(1f, mapPadding);
        float height = bounds.max.y + Mathf.Max(1f, cameraHeight);
        minimapCamera.farClipPlane = height - bounds.min.y + 10f;
        minimapCamera.transform.SetPositionAndRotation(
            new Vector3(bounds.center.x, height, bounds.center.z),
            Quaternion.Euler(90f, 0f, 0f));
    }

    private void UpdateExploration()
    {
        if (target == null || minimapCamera == null ||playerMarker == null ||
            explorationPixels == null || explorationTexture == null)
        {
            return;
        }

        Vector3 position = minimapCamera.WorldToViewportPoint(target.position);
        playerMarker.anchoredPosition = new Vector2(
            (Mathf.Clamp01(position.x) - 0.5f) * displaySize.x,
            (Mathf.Clamp01(position.y) - 0.5f) * displaySize.y);

        float radius = Mathf.Max(0.1f, revealRadius) /
            (minimapCamera.orthographicSize * 2f) * ExplorationResolution;
        float centerX = position.x * ExplorationResolution;
        float centerY = position.y * ExplorationResolution;
        int minX = Mathf.Max(0, Mathf.FloorToInt(centerX - radius));
        int maxX = Mathf.Min(ExplorationResolution - 1, Mathf.CeilToInt(centerX + radius));
        int minY = Mathf.Max(0, Mathf.FloorToInt(centerY - radius));
        int maxY = Mathf.Min(ExplorationResolution - 1, Mathf.CeilToInt(centerY + radius));
        bool changed = false;
        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                float dx = x + 0.5f - centerX;
                float dy = y + 0.5f - centerY;
                int index = y * ExplorationResolution + x;
                if (dx * dx + dy * dy <= radius * radius && explorationPixels[index].a != 0)
                {
                    explorationPixels[index].a = 0;
                    changed = true;
                }
            }
        }

        if (changed)
        {
            explorationTexture.SetPixels32(explorationPixels);
            explorationTexture.Apply(false);
        }
    }

    private void OnDestroy()
    {
        if (explorationTexture != null)
        {
            Destroy(explorationTexture);
        }

        if (minimapTexture == null)
        {
            return;
        }

        minimapTexture.Release();
        Destroy(minimapTexture);
    }
}
