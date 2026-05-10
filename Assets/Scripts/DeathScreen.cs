using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(CanvasGroup))]
public sealed class DeathScreen : MonoBehaviour
{
    [Header("Text Fields")]
    [SerializeField] private TMP_Text editionNumberText;
    [SerializeField] private TMP_Text causeTagText;
    [SerializeField] private TMP_Text headlineText;
    [SerializeField] private TMP_Text headlineEnText;
    [SerializeField] private TMP_Text subheadText;
    [SerializeField] private TMP_Text subheadEnText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text coinsText;
    [SerializeField] private TMP_Text flavourText;
    [SerializeField] private TMP_Text deathCounterText;

    [Header("Buttons")]
    [SerializeField] private Button retryButton;
    [SerializeField] private Button shareButton;

    [Header("Animation")]
    [SerializeField] private float fadeDuration = 0.35f;

    [Header("Layout")]
    [SerializeField] private RectTransform newspaperPanel;
    [SerializeField, Range(0.5f, 1f)] private float webTextScale = 0.75f;
    [SerializeField, Range(0.55f, 1f)] private float webLayoutScale = 0.66f;
    [SerializeField, Range(1f, 1.8f)] private float webWidthScale = 1.4f;

    private const string RunCountKey = "DeathScreen.RunCount";
    private const float FullyVisibleAlpha = 0.99f;
    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;
    private bool activatingForShow;
    private bool retryRequested;
    private int lastShareFrame = -1;
    private TMP_Text[] newspaperTexts;
    private float[] originalFontSizes;
    private float[] originalFontSizeMins;
    private float[] originalFontSizeMaxes;
    private LayoutElement[] newspaperLayoutElements;
    private LayoutElementSize[] originalLayoutElementSizes;
    private HorizontalOrVerticalLayoutGroup[] newspaperLayoutGroups;
    private LayoutGroupMetrics[] originalLayoutGroupMetrics;
    private RectTransform[] newspaperLayoutRects;
    private Vector2[] originalLayoutRectSizeDeltas;

    private struct LayoutElementSize
    {
        public float MinHeight;
        public float PreferredHeight;
    }

    private struct LayoutGroupMetrics
    {
        public int PaddingLeft;
        public int PaddingRight;
        public int PaddingTop;
        public int PaddingBottom;
        public float Spacing;
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (retryButton != null)
        {
            retryButton.onClick.AddListener(OnRetry);
        }
        else
        {
            Debug.LogWarning("[DeathScreen] Retry button is not assigned; retry taps cannot fire.", this);
        }

        if (shareButton != null)
        {
            shareButton.onClick.AddListener(OnShare);
        }
        else
        {
            Debug.LogWarning("[DeathScreen] Share button is not assigned; share taps cannot fire.", this);
        }

        if (!activatingForShow)
        {
            HideImmediate();
            gameObject.SetActive(true);
            ScaleNewspaperToScreen();
            HideImmediate();
        }
        else
        {
            ScaleNewspaperToScreen();
        }
    }

    private void OnDestroy()
    {
        if (retryButton != null)
        {
            retryButton.onClick.RemoveListener(OnRetry);
        }

        if (shareButton != null)
        {
            shareButton.onClick.RemoveListener(OnShare);
        }
    }

    private void Update()
    {
        if (!CanUsePointerFallback() || !TryGetPointerUpPosition(out Vector2 screenPosition))
        {
            return;
        }

        bool retryHit = IsButtonHit(retryButton, screenPosition);
        bool shareHit = IsButtonHit(shareButton, screenPosition);

        if (retryHit)
        {
            OnRetry();
            return;
        }

        if (shareHit)
        {
            OnShare();
        }
    }

    public void Show(DeathData data, int score, int coins)
    {
        activatingForShow = true;
        gameObject.SetActive(true);
        activatingForShow = false;

        canvasGroup = GetComponent<CanvasGroup>();

        int runCount = PlayerPrefs.GetInt(RunCountKey, 0) + 1;
        PlayerPrefs.SetInt(RunCountKey, runCount);
        PlayerPrefs.Save();

        SetText(editionNumberText, $"VOL. 1  NO. {runCount}");
        SetText(causeTagText, $"PUNCA: {(data?.CauseName ?? "MYVI").ToUpperInvariant()}");
        SetText(headlineText, data?.Headline ?? string.Empty);
        SetText(headlineEnText, data?.HeadlineEn ?? string.Empty);
        SetText(subheadText, data?.Subhead ?? string.Empty);
        SetText(subheadEnText, data?.SubheadEn ?? string.Empty);
        SetText(scoreText, Mathf.Max(0, score).ToString());
        SetText(coinsText, Mathf.Max(0, coins).ToString());
        SetText(flavourText, data?.FlavourText ?? string.Empty);
        SetText(deathCounterText, $"Kematian ke-{runCount} hari ini");

        ScaleNewspaperToScreen();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeIn());
    }

    public void HideImmediate()
    {
        canvasGroup = GetComponent<CanvasGroup>();

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
            fadeRoutine = null;
        }

        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = false;
        gameObject.SetActive(false);
    }

    private IEnumerator FadeIn()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.interactable = false;
        canvasGroup.blocksRaycasts = true;
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            canvasGroup.alpha = Mathf.Clamp01(elapsed / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = 1f;
        canvasGroup.interactable = true;
        canvasGroup.blocksRaycasts = true;
        fadeRoutine = null;
    }

    private void ScaleNewspaperToScreen()
    {
        if (newspaperPanel == null)
        {
            Debug.LogWarning("[DeathScreen] newspaperPanel is null - drag Newspaper into the slot in Inspector.");
            return;
        }

        Canvas canvas = GetComponentInParent<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("[DeathScreen] No parent Canvas found.");
            return;
        }

        RectTransform canvasRect = canvas.GetComponent<RectTransform>();
        float canvasWidth = canvasRect.rect.width;
        float canvasHeight = canvasRect.rect.height;

        float horizontalPadding = 0.88f;
        float verticalPadding = 0.92f;

        float maxWidth = canvasWidth * horizontalPadding;
        float maxHeight = canvasHeight * verticalPadding;

        float originalWidth = 880f;
        float originalHeight = 1560f;

        bool useWebLayout = ShouldUseWebNewspaperLayout(canvasWidth, canvasHeight);
        if (useWebLayout)
        {
            originalWidth *= webWidthScale;
        }

        float aspectRatio = originalHeight / originalWidth;

        float targetWidth = maxWidth;
        float targetHeight = targetWidth * aspectRatio;

        if (targetHeight > maxHeight)
        {
            targetHeight = maxHeight;
            targetWidth = targetHeight / aspectRatio;
        }

        newspaperPanel.sizeDelta = new Vector2(targetWidth, targetHeight);
        ApplyNewspaperLayoutScale(useWebLayout);
        ApplyNewspaperTextScale(useWebLayout);
        LayoutRebuilder.ForceRebuildLayoutImmediate(newspaperPanel);
    }

    private static bool ShouldUseWebNewspaperLayout(float canvasWidth, float canvasHeight)
    {
        return Application.platform == RuntimePlatform.WebGLPlayer || canvasWidth > canvasHeight;
    }

    private void ApplyNewspaperTextScale(bool useWebLayout)
    {
        if (!TryCacheNewspaperTextSizes())
        {
            return;
        }

        float textScale = useWebLayout ? webTextScale : 1f;

        for (int index = 0; index < newspaperTexts.Length; index++)
        {
            TMP_Text text = newspaperTexts[index];
            if (text == null)
            {
                continue;
            }

            text.fontSize = originalFontSizes[index] * textScale;
            text.fontSizeMin = originalFontSizeMins[index] * textScale;
            text.fontSizeMax = originalFontSizeMaxes[index] * textScale;
        }
    }

    private bool TryCacheNewspaperTextSizes()
    {
        if (newspaperTexts != null)
        {
            return true;
        }

        if (newspaperPanel == null)
        {
            return false;
        }

        newspaperTexts = newspaperPanel.GetComponentsInChildren<TMP_Text>(true);
        originalFontSizes = new float[newspaperTexts.Length];
        originalFontSizeMins = new float[newspaperTexts.Length];
        originalFontSizeMaxes = new float[newspaperTexts.Length];

        for (int index = 0; index < newspaperTexts.Length; index++)
        {
            TMP_Text text = newspaperTexts[index];
            if (text == null)
            {
                continue;
            }

            originalFontSizes[index] = text.fontSize;
            originalFontSizeMins[index] = text.fontSizeMin;
            originalFontSizeMaxes[index] = text.fontSizeMax;
        }

        return true;
    }

    private void ApplyNewspaperLayoutScale(bool useWebLayout)
    {
        if (!TryCacheNewspaperLayoutSizes())
        {
            return;
        }

        float layoutScale = useWebLayout ? webLayoutScale : 1f;

        for (int index = 0; index < newspaperLayoutElements.Length; index++)
        {
            LayoutElement layoutElement = newspaperLayoutElements[index];
            if (layoutElement == null)
            {
                continue;
            }

            LayoutElementSize originalSize = originalLayoutElementSizes[index];
            layoutElement.minHeight = ScalePositive(originalSize.MinHeight, layoutScale);
            layoutElement.preferredHeight = ScalePositive(originalSize.PreferredHeight, layoutScale);
        }

        for (int index = 0; index < newspaperLayoutRects.Length; index++)
        {
            RectTransform rectTransform = newspaperLayoutRects[index];
            if (rectTransform == null)
            {
                continue;
            }

            Vector2 originalSizeDelta = originalLayoutRectSizeDeltas[index];
            rectTransform.sizeDelta = new Vector2(
                originalSizeDelta.x,
                ScalePositive(originalSizeDelta.y, layoutScale));
        }

        for (int index = 0; index < newspaperLayoutGroups.Length; index++)
        {
            HorizontalOrVerticalLayoutGroup layoutGroup = newspaperLayoutGroups[index];
            if (layoutGroup == null)
            {
                continue;
            }

            LayoutGroupMetrics originalMetrics = originalLayoutGroupMetrics[index];
            layoutGroup.padding = new RectOffset(
                ScalePositive(originalMetrics.PaddingLeft, layoutScale),
                ScalePositive(originalMetrics.PaddingRight, layoutScale),
                ScalePositive(originalMetrics.PaddingTop, layoutScale),
                ScalePositive(originalMetrics.PaddingBottom, layoutScale));
            layoutGroup.spacing = ScalePositive(originalMetrics.Spacing, layoutScale);
        }
    }

    private bool TryCacheNewspaperLayoutSizes()
    {
        if (newspaperLayoutElements != null && newspaperLayoutGroups != null && newspaperLayoutRects != null)
        {
            return true;
        }

        if (newspaperPanel == null)
        {
            return false;
        }

        newspaperLayoutElements = newspaperPanel.GetComponentsInChildren<LayoutElement>(true);
        originalLayoutElementSizes = new LayoutElementSize[newspaperLayoutElements.Length];

        for (int index = 0; index < newspaperLayoutElements.Length; index++)
        {
            LayoutElement layoutElement = newspaperLayoutElements[index];
            if (layoutElement == null)
            {
                continue;
            }

            originalLayoutElementSizes[index] = new LayoutElementSize
            {
                MinHeight = layoutElement.minHeight,
                PreferredHeight = layoutElement.preferredHeight
            };
        }

        RectTransform[] allRectTransforms = newspaperPanel.GetComponentsInChildren<RectTransform>(true);
        newspaperLayoutRects = new RectTransform[Mathf.Max(0, allRectTransforms.Length - 1)];
        originalLayoutRectSizeDeltas = new Vector2[newspaperLayoutRects.Length];

        int layoutRectIndex = 0;
        for (int index = 0; index < allRectTransforms.Length; index++)
        {
            RectTransform rectTransform = allRectTransforms[index];
            if (rectTransform == null || rectTransform == newspaperPanel)
            {
                continue;
            }

            newspaperLayoutRects[layoutRectIndex] = rectTransform;
            originalLayoutRectSizeDeltas[layoutRectIndex] = rectTransform.sizeDelta;
            layoutRectIndex++;
        }

        newspaperLayoutGroups = newspaperPanel.GetComponentsInChildren<HorizontalOrVerticalLayoutGroup>(true);
        originalLayoutGroupMetrics = new LayoutGroupMetrics[newspaperLayoutGroups.Length];

        for (int index = 0; index < newspaperLayoutGroups.Length; index++)
        {
            HorizontalOrVerticalLayoutGroup layoutGroup = newspaperLayoutGroups[index];
            if (layoutGroup == null)
            {
                continue;
            }

            RectOffset padding = layoutGroup.padding;
            originalLayoutGroupMetrics[index] = new LayoutGroupMetrics
            {
                PaddingLeft = padding.left,
                PaddingRight = padding.right,
                PaddingTop = padding.top,
                PaddingBottom = padding.bottom,
                Spacing = layoutGroup.spacing
            };
        }

        return true;
    }

    private static float ScalePositive(float value, float scale)
    {
        return value > 0f ? value * scale : value;
    }

    private static int ScalePositive(int value, float scale)
    {
        return value > 0 ? Mathf.RoundToInt(value * scale) : value;
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null)
        {
            target.text = value;
        }
    }

    private bool CanUsePointerFallback()
    {
        return isActiveAndEnabled &&
               canvasGroup != null &&
               canvasGroup.alpha > FullyVisibleAlpha &&
               canvasGroup.interactable &&
               canvasGroup.blocksRaycasts;
    }

    private static bool TryGetPointerUpPosition(out Vector2 screenPosition)
    {
        if (Input.touchCount > 0)
        {
            Touch touch = Input.GetTouch(0);
            if (touch.phase == TouchPhase.Ended)
            {
                screenPosition = touch.position;
                return true;
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            screenPosition = Input.mousePosition;
            return true;
        }

        screenPosition = default;
        return false;
    }

    private bool IsButtonHit(Button button, Vector2 screenPosition)
    {
        if (button == null || !button.gameObject.activeInHierarchy || !button.interactable)
        {
            return false;
        }

        RectTransform rectTransform = button.transform as RectTransform;
        if (rectTransform == null)
        {
            return false;
        }

        Canvas parentCanvas = button.GetComponentInParent<Canvas>();
        Camera eventCamera = parentCanvas != null && parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
            ? parentCanvas.worldCamera
            : null;

        return RectTransformUtility.RectangleContainsScreenPoint(rectTransform, screenPosition, eventCamera);
    }

    private void OnRetry()
    {
        if (retryRequested)
        {
            return;
        }

        retryRequested = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    private void OnShare()
    {
        if (lastShareFrame == Time.frameCount)
        {
            return;
        }

        lastShareFrame = Time.frameCount;
    }
}
