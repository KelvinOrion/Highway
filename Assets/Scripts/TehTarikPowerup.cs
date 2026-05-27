using System.Collections;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
public sealed class TehTarikPowerup : PowerupBase
{
    private const float MinSpeedMultiplier = 0.01f;
    private const float CameraShakeDuration = 0.3f;
    private const float CameraZoomMultiplier = 0.85f;
    private const float CameraZoomDuration = 0.3f;
    private const float SpeedLineImageAlpha = 0.4f;
    private const float SpeedLineFadeInDuration = 0.2f;
    private const float SpeedLineFadeOutDuration = 0.3f;

    [SerializeField] private float speedMultiplier = 1.6f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private Texture speedLinesTexture;
    [SerializeField] private float speedLineARotationSpeed = 12f;
    [SerializeField] private float speedLineBRotationSpeed = -9f;

    private static Coroutine activeRoutine;
    private static Coroutine activeZoomRoutine;
    private static Coroutine activeSpeedLineRotationRoutine;
    private static Coroutine activeSpeedLineFadeRoutine;
    private static GameManager activeManager;
    private static MonoBehaviour activeZoomHost;
    private static MonoBehaviour activeSpeedLineHost;
    private static Camera activeCamera;
    private static GameObject activeSpeedLineOverlay;
    private static RawImage activeSpeedLineA;
    private static RawImage activeSpeedLineB;
    private static RectTransform activeSpeedLineATransform;
    private static RectTransform activeSpeedLineBTransform;
    private static Texture2D placeholderSpeedLinesTexture;
    private static float originalMoveDuration;
    private static float originalCameraZoom;
    private static bool originalCameraWasOrthographic;
    private static bool hasOriginalCameraZoom;

    public static bool IsActive { get; private set; }

    public static void ResetRuntimeState()
    {
        if (activeRoutine != null && activeManager != null)
        {
            activeManager.StopCoroutine(activeRoutine);
        }

        StopActiveZoomRoutine();
        RestoreCameraZoomImmediate();
        HideSpeedLinesImmediate();

        activeRoutine = null;
        activeManager = null;
        activeCamera = null;
        originalCameraZoom = 0f;
        originalMoveDuration = 0f;
        hasOriginalCameraZoom = false;
        IsActive = false;
    }

    protected override void Activate(GameObject player)
    {
        GameManager manager = FindFirstObjectByType<GameManager>();
        if (manager == null)
        {
            Debug.LogWarning($"{nameof(TehTarikPowerup)} could not find {nameof(GameManager)} to change hop speed.", this);
            return;
        }

        if (activeRoutine == null || activeManager == null)
        {
            originalMoveDuration = manager.MoveDuration;
        }
        else
        {
            activeManager.StopCoroutine(activeRoutine);
        }

        activeManager = manager;
        IsActive = true;

        float multiplier = Mathf.Max(MinSpeedMultiplier, speedMultiplier);
        manager.SetMoveDuration(originalMoveDuration / multiplier);
        manager.PlayCameraShake(CameraShakeDuration, shakeMagnitude);
        ShowSpeedLines(manager, speedLinesTexture, speedLineARotationSpeed, speedLineBRotationSpeed);
        StartCameraZoom(manager, Camera.main);

        activeRoutine = manager.StartCoroutine(RestoreAfterDuration(manager, Mathf.Max(0f, duration), originalMoveDuration));
    }

    private static IEnumerator RestoreAfterDuration(GameManager manager, float seconds, float restoreMoveDuration)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (manager != null)
        {
            manager.SetMoveDuration(restoreMoveDuration);
        }

        HideSpeedLines(manager);
        StartCameraZoomRestore(manager);

        if (activeManager == manager)
        {
            activeRoutine = null;
            activeManager = null;
            originalMoveDuration = 0f;
            IsActive = false;
        }
    }

    private static void ShowSpeedLines(MonoBehaviour host, Texture texture, float speedLineASpeed, float speedLineBSpeed)
    {
        if (host == null || !ResolveSpeedLinesOverlay())
        {
            return;
        }

        StopSpeedLineCoroutines();
        activeSpeedLineHost = host;

        Texture overlayTexture = texture != null ? texture : GetPlaceholderSpeedLinesTexture();
        ConfigureSpeedLineImage(activeSpeedLineA, overlayTexture, 0f);
        ConfigureSpeedLineImage(activeSpeedLineB, overlayTexture, 0f);

        activeSpeedLineOverlay.SetActive(true);
        activeSpeedLineOverlay.transform.SetAsLastSibling();

        activeSpeedLineRotationRoutine = host.StartCoroutine(RotateSpeedLines(speedLineASpeed, speedLineBSpeed));
        activeSpeedLineFadeRoutine = host.StartCoroutine(FadeSpeedLines(0f, SpeedLineImageAlpha, SpeedLineFadeInDuration, disableOnComplete: false));
    }

    private static void HideSpeedLines(MonoBehaviour host)
    {
        if (host == null || activeSpeedLineOverlay == null)
        {
            HideSpeedLinesImmediate();
            return;
        }

        if (!activeSpeedLineOverlay.activeSelf)
        {
            StopSpeedLineCoroutines();
            activeSpeedLineHost = null;
            return;
        }

        if (activeSpeedLineFadeRoutine != null && activeSpeedLineHost != null)
        {
            activeSpeedLineHost.StopCoroutine(activeSpeedLineFadeRoutine);
            activeSpeedLineFadeRoutine = null;
        }

        activeSpeedLineHost = host;
        activeSpeedLineFadeRoutine = host.StartCoroutine(FadeSpeedLines(GetSpeedLineAlpha(activeSpeedLineA), 0f, SpeedLineFadeOutDuration, disableOnComplete: true));
    }

    private static void HideSpeedLinesImmediate()
    {
        StopSpeedLineCoroutines();
        SetSpeedLineAlpha(0f);

        if (activeSpeedLineOverlay != null)
        {
            activeSpeedLineOverlay.SetActive(false);
        }

        activeSpeedLineHost = null;
    }

    private static bool ResolveSpeedLinesOverlay()
    {
        if (activeSpeedLineOverlay != null &&
            activeSpeedLineA != null &&
            activeSpeedLineB != null &&
            activeSpeedLineATransform != null &&
            activeSpeedLineBTransform != null)
        {
            return true;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return false;
        }

        activeSpeedLineOverlay = new GameObject("SpeedLineOverlay", typeof(RectTransform));
        activeSpeedLineOverlay.transform.SetParent(canvas.transform, false);
        StretchToFullScreen(activeSpeedLineOverlay.GetComponent<RectTransform>());

        activeSpeedLineA = CreateSpeedLineImage("SpeedLineA", activeSpeedLineOverlay.transform, out activeSpeedLineATransform);
        activeSpeedLineB = CreateSpeedLineImage("SpeedLineB", activeSpeedLineOverlay.transform, out activeSpeedLineBTransform);

        activeSpeedLineOverlay.SetActive(false);
        return true;
    }

    private static RawImage CreateSpeedLineImage(string name, Transform parent, out RectTransform rectTransform)
    {
        GameObject imageObject = new(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(parent, false);

        rectTransform = imageObject.GetComponent<RectTransform>();
        StretchToFullScreen(rectTransform);

        RawImage image = imageObject.GetComponent<RawImage>();
        image.raycastTarget = false;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        image.color = new Color(1f, 1f, 1f, 0f);
        return image;
    }

    private static void StretchToFullScreen(RectTransform rectTransform)
    {
        if (rectTransform == null)
        {
            return;
        }

        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;
    }

    private static void ConfigureSpeedLineImage(RawImage image, Texture texture, float alpha)
    {
        if (image == null)
        {
            return;
        }

        image.texture = texture;
        image.uvRect = new Rect(0f, 0f, 1f, 1f);
        image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
    }

    private static IEnumerator RotateSpeedLines(float speedLineASpeed, float speedLineBSpeed)
    {
        while (activeSpeedLineOverlay != null && activeSpeedLineOverlay.activeSelf)
        {
            float deltaTime = Time.deltaTime;

            if (activeSpeedLineATransform != null)
            {
                activeSpeedLineATransform.Rotate(0f, 0f, speedLineASpeed * deltaTime);
            }

            if (activeSpeedLineBTransform != null)
            {
                activeSpeedLineBTransform.Rotate(0f, 0f, speedLineBSpeed * deltaTime);
            }

            yield return null;
        }

        activeSpeedLineRotationRoutine = null;
    }

    private static IEnumerator FadeSpeedLines(float startAlpha, float targetAlpha, float seconds, bool disableOnComplete)
    {
        if (seconds <= 0f)
        {
            SetSpeedLineAlpha(targetAlpha);
            CompleteSpeedLineFade(disableOnComplete);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds)
        {
            float t = Mathf.Clamp01(elapsed / seconds);
            SetSpeedLineAlpha(Mathf.Lerp(startAlpha, targetAlpha, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetSpeedLineAlpha(targetAlpha);
        CompleteSpeedLineFade(disableOnComplete);
    }

    private static void CompleteSpeedLineFade(bool disableOnComplete)
    {
        if (disableOnComplete)
        {
            if (activeSpeedLineRotationRoutine != null && activeSpeedLineHost != null)
            {
                activeSpeedLineHost.StopCoroutine(activeSpeedLineRotationRoutine);
                activeSpeedLineRotationRoutine = null;
            }

            if (activeSpeedLineOverlay != null)
            {
                activeSpeedLineOverlay.SetActive(false);
            }

            activeSpeedLineHost = null;
        }

        activeSpeedLineFadeRoutine = null;
    }

    private static void SetSpeedLineAlpha(float alpha)
    {
        alpha = Mathf.Clamp01(alpha);
        SetSpeedLineAlpha(activeSpeedLineA, alpha);
        SetSpeedLineAlpha(activeSpeedLineB, alpha);
    }

    private static void SetSpeedLineAlpha(RawImage image, float alpha)
    {
        if (image == null)
        {
            return;
        }

        Color color = image.color;
        color.a = alpha;
        image.color = color;
    }

    private static float GetSpeedLineAlpha(RawImage image)
    {
        return image != null ? image.color.a : 0f;
    }

    private static void StopSpeedLineCoroutines()
    {
        if (activeSpeedLineRotationRoutine != null && activeSpeedLineHost != null)
        {
            activeSpeedLineHost.StopCoroutine(activeSpeedLineRotationRoutine);
        }

        if (activeSpeedLineFadeRoutine != null && activeSpeedLineHost != null)
        {
            activeSpeedLineHost.StopCoroutine(activeSpeedLineFadeRoutine);
        }

        activeSpeedLineRotationRoutine = null;
        activeSpeedLineFadeRoutine = null;
    }

    private static Canvas ResolveCanvas()
    {
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.isActiveAndEnabled && canvas.renderMode != RenderMode.WorldSpace)
            {
                return canvas;
            }
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.renderMode != RenderMode.WorldSpace)
            {
                return canvas;
            }
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas != null && canvas.isActiveAndEnabled)
            {
                return canvas;
            }
        }

        foreach (Canvas canvas in canvases)
        {
            if (canvas != null)
            {
                return canvas;
            }
        }

        GameObject canvasObject = new("TehTarikSpeedLinesCanvas", typeof(Canvas), typeof(CanvasScaler));

        Canvas createdCanvas = canvasObject.GetComponent<Canvas>();
        createdCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        createdCanvas.sortingOrder = short.MaxValue;
        return createdCanvas;
    }

    private static Texture2D GetPlaceholderSpeedLinesTexture()
    {
        if (placeholderSpeedLinesTexture != null)
        {
            return placeholderSpeedLinesTexture;
        }

        placeholderSpeedLinesTexture = new Texture2D(1, 1)
        {
            name = "Teh Tarik Speed Lines Placeholder",
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Point
        };
        placeholderSpeedLinesTexture.SetPixel(0, 0, Color.white);
        placeholderSpeedLinesTexture.Apply();
        return placeholderSpeedLinesTexture;
    }

    private static void StartCameraZoom(GameManager manager, Camera camera)
    {
        if (manager == null || camera == null)
        {
            return;
        }

        if (!hasOriginalCameraZoom || activeCamera != camera)
        {
            activeCamera = camera;
            originalCameraWasOrthographic = camera.orthographic;
            originalCameraZoom = GetCameraZoom(camera);
            hasOriginalCameraZoom = true;
        }

        StopActiveZoomRoutine();

        float currentZoom = GetCameraZoom(camera);
        float targetZoom = originalCameraZoom * CameraZoomMultiplier;
        activeZoomHost = manager;
        activeZoomRoutine = manager.StartCoroutine(LerpCameraZoom(camera, currentZoom, targetZoom, CameraZoomDuration, clearOriginalOnComplete: false));
    }

    private static void StartCameraZoomRestore(GameManager manager)
    {
        if (manager == null || activeCamera == null || !hasOriginalCameraZoom)
        {
            return;
        }

        StopActiveZoomRoutine();

        activeZoomHost = manager;
        activeZoomRoutine = manager.StartCoroutine(LerpCameraZoom(
            activeCamera,
            GetCameraZoom(activeCamera),
            originalCameraZoom,
            CameraZoomDuration,
            clearOriginalOnComplete: true));
    }

    private static IEnumerator LerpCameraZoom(Camera camera, float startZoom, float targetZoom, float seconds, bool clearOriginalOnComplete)
    {
        if (camera == null)
        {
            ClearZoomRoutineState(clearOriginalOnComplete);
            yield break;
        }

        if (seconds <= 0f)
        {
            SetCameraZoom(camera, targetZoom);
            ClearZoomRoutineState(clearOriginalOnComplete);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds && camera != null)
        {
            float t = Mathf.Clamp01(elapsed / seconds);
            SetCameraZoom(camera, Mathf.Lerp(startZoom, targetZoom, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (camera != null)
        {
            SetCameraZoom(camera, targetZoom);
        }

        ClearZoomRoutineState(clearOriginalOnComplete);
    }

    private static void StopActiveZoomRoutine()
    {
        if (activeZoomRoutine != null && activeZoomHost != null)
        {
            activeZoomHost.StopCoroutine(activeZoomRoutine);
        }

        activeZoomRoutine = null;
        activeZoomHost = null;
    }

    private static void RestoreCameraZoomImmediate()
    {
        if (activeCamera == null || !hasOriginalCameraZoom)
        {
            return;
        }

        if (originalCameraWasOrthographic)
        {
            activeCamera.orthographicSize = Mathf.Max(0.01f, originalCameraZoom);
            return;
        }

        activeCamera.fieldOfView = Mathf.Clamp(originalCameraZoom, 1f, 179f);
    }

    private static float GetCameraZoom(Camera camera)
    {
        return camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
    }

    private static void SetCameraZoom(Camera camera, float zoom)
    {
        if (camera.orthographic)
        {
            camera.orthographicSize = Mathf.Max(0.01f, zoom);
            return;
        }

        camera.fieldOfView = Mathf.Clamp(zoom, 1f, 179f);
    }

    private static void ClearZoomRoutineState(bool clearOriginal)
    {
        activeZoomRoutine = null;
        activeZoomHost = null;

        if (!clearOriginal)
        {
            return;
        }

        activeCamera = null;
        originalCameraZoom = 0f;
        hasOriginalCameraZoom = false;
    }
}
