using UnityEngine;
using UnityEngine.UI;

// Retains a formerly independent canvas's screen layout under the main Canvas.
[ExecuteAlways]
[RequireComponent(typeof(Canvas), typeof(CanvasScaler))]
public class NestedCanvasLayout : MonoBehaviour
{
    private Canvas canvas;
    private CanvasScaler scaler;
    private RectTransform rect;

    public float LayoutScaleFactor
    {
        get
        {
            CacheComponents();
            Vector2 screenSize = canvas.renderingDisplaySize;
            Vector2 reference = scaler.referenceResolution;
            float widthScale = screenSize.x / reference.x;
            float heightScale = screenSize.y / reference.y;

            switch (scaler.screenMatchMode)
            {
                case CanvasScaler.ScreenMatchMode.Expand:
                    return Mathf.Min(widthScale, heightScale);
                case CanvasScaler.ScreenMatchMode.Shrink:
                    return Mathf.Max(widthScale, heightScale);
                default:
                    return Mathf.Pow(2f, Mathf.Lerp(
                        Mathf.Log(widthScale, 2f),
                        Mathf.Log(heightScale, 2f),
                        scaler.matchWidthOrHeight));
            }
        }
    }

    private void OnEnable()
    {
        Canvas.preWillRenderCanvases += PreserveLayout;
        PreserveLayout();
    }

    private void OnDisable()
    {
        Canvas.preWillRenderCanvases -= PreserveLayout;
    }

    private void LateUpdate()
    {
        PreserveLayout();
    }

    private void CacheComponents()
    {
        if (canvas == null) canvas = GetComponent<Canvas>();
        if (scaler == null) scaler = GetComponent<CanvasScaler>();
        if (rect == null) rect = GetComponent<RectTransform>();
    }

    private void PreserveLayout()
    {
        CacheComponents();
        if (canvas.isRootCanvas ||
            scaler.uiScaleMode != CanvasScaler.ScaleMode.ScaleWithScreenSize ||
            canvas.renderingDisplaySize.x <= 0f || canvas.renderingDisplaySize.y <= 0f)
            return;

        float scale = LayoutScaleFactor;
        float parentScale = canvas.rootCanvas.scaleFactor;
        if (scale <= 0f || parentScale <= 0f)
            return;

        // Nested CanvasScalers do not run, so retain their original pixel scale
        // and sorting order without changing any text or button rectangles.
        Vector2 center = new Vector2(0.5f, 0.5f);
        Vector2 size = canvas.renderingDisplaySize / scale;
        Vector3 localScale = Vector3.one * (scale / parentScale);
        if (!canvas.overrideSorting) canvas.overrideSorting = true;
        if (rect.anchorMin != center) rect.anchorMin = center;
        if (rect.anchorMax != center) rect.anchorMax = center;
        if (rect.pivot != center) rect.pivot = center;
        if (rect.anchoredPosition != Vector2.zero) rect.anchoredPosition = Vector2.zero;
        if (rect.sizeDelta != size) rect.sizeDelta = size;
        if (rect.localScale != localScale) rect.localScale = localScale;
    }
}
