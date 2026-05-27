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
    private const float SpeedLinesAlpha = 0.6f;

    [SerializeField] private float speedMultiplier = 1.6f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private Texture speedLinesTexture;

    private static Coroutine activeRoutine;
    private static Coroutine activeZoomRoutine;
    private static GameManager activeManager;
    private static MonoBehaviour activeZoomHost;
    private static Camera activeCamera;
    private static RawImage activeSpeedLinesOverlay;
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
        HideSpeedLines();

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
        ShowSpeedLines(speedLinesTexture);
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

        HideSpeedLines();
        StartCameraZoomRestore(manager);

        if (activeManager == manager)
        {
            activeRoutine = null;
            activeManager = null;
            originalMoveDuration = 0f;
            IsActive = false;
        }
    }

    private static void ShowSpeedLines(Texture texture)
    {
        RawImage overlay = ResolveSpeedLinesOverlay();
        if (overlay == null)
        {
            return;
        }

        overlay.texture = texture != null ? texture : GetPlaceholderSpeedLinesTexture();
        overlay.color = new Color(1f, 1f, 1f, SpeedLinesAlpha);
        overlay.gameObject.SetActive(true);
        overlay.transform.SetAsLastSibling();
    }

    private static void HideSpeedLines()
    {
        if (activeSpeedLinesOverlay != null)
        {
            activeSpeedLinesOverlay.gameObject.SetActive(false);
        }
    }

    private static RawImage ResolveSpeedLinesOverlay()
    {
        if (activeSpeedLinesOverlay != null)
        {
            return activeSpeedLinesOverlay;
        }

        Canvas canvas = ResolveCanvas();
        if (canvas == null)
        {
            return null;
        }

        GameObject overlayObject = new("TehTarik_SpeedLinesOverlay", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        overlayObject.transform.SetParent(canvas.transform, false);

        RectTransform rectTransform = overlayObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        activeSpeedLinesOverlay = overlayObject.GetComponent<RawImage>();
        activeSpeedLinesOverlay.raycastTarget = false;
        activeSpeedLinesOverlay.gameObject.SetActive(false);
        return activeSpeedLinesOverlay;
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
