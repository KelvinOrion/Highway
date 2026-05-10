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
    [SerializeField] private float mobileScaleFactor = 0.92f;
    [SerializeField] private float desktopScaleFactor = 0.65f;
    [SerializeField] private float mobileWidthThreshold = 600f;

    private const string RunCountKey = "DeathScreen.RunCount";
    private const float FullyVisibleAlpha = 0.99f;
    private CanvasGroup canvasGroup;
    private Coroutine fadeRoutine;
    private bool activatingForShow;
    private bool retryRequested;
    private int lastShareFrame = -1;

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
            newspaperPanel = transform as RectTransform;
        }

        if (newspaperPanel == null)
        {
            return;
        }

        float screenWidth = Screen.width;
        float scaleFactor = screenWidth < mobileWidthThreshold
            ? mobileScaleFactor
            : desktopScaleFactor;

        Canvas parentCanvas = GetComponentInParent<Canvas>();
        if (parentCanvas == null)
        {
            return;
        }

        RectTransform canvasRect = parentCanvas.GetComponent<RectTransform>();
        if (canvasRect == null)
        {
            return;
        }

        float currentWidth = newspaperPanel.rect.width;
        float currentHeight = newspaperPanel.rect.height;
        if (currentWidth <= 0f || currentHeight <= 0f)
        {
            currentWidth = newspaperPanel.sizeDelta.x;
            currentHeight = newspaperPanel.sizeDelta.y;
        }

        if (currentWidth <= 0f || currentHeight <= 0f)
        {
            return;
        }

        float targetWidth = canvasRect.rect.width * scaleFactor;
        float ratio = currentHeight / currentWidth;
        float targetHeight = targetWidth * ratio;

        newspaperPanel.sizeDelta = new Vector2(targetWidth, targetHeight);
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
