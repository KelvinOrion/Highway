using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Applies camera + UI layout rules from device capabilities and aspect ratio.
/// Architecture note:
/// - Uses input-device capabilities (touch/keyboard/mouse) to infer mobile-vs-desktop intent.
/// - Keeps world scale stable by preserving a reference world height and compensating for narrow aspects.
/// - Keeps UI readable by changing CanvasScaler match mode per orientation.
/// </summary>
public class ResponsiveLayoutController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Camera targetCamera;
    [SerializeField] private CanvasScaler[] canvasScalers;

    [Header("World scale")]
    [SerializeField] private float referenceWorldHeight = 12f;
    [SerializeField] private float landscapeReferenceAspect = 16f / 9f;
    [SerializeField] private float portraitReferenceAspect = 9f / 16f;

    [Header("UI scaling")]
    [SerializeField] private Vector2 landscapeReferenceResolution = new(1920f, 1080f);
    [SerializeField] private Vector2 portraitReferenceResolution = new(1080f, 1920f);

    private bool isPortraitLayout;
    private int lastWidth;
    private int lastHeight;

    private void Awake()
    {
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        ApplyLayout(force: true);
    }

    private void Update()
    {
        if (Screen.width != lastWidth || Screen.height != lastHeight)
        {
            ApplyLayout(force: false);
        }
    }

    private void ApplyLayout(bool force)
    {
        bool usePortrait = IsMobileCapabilitySet();

        if (!force && usePortrait == isPortraitLayout && Screen.width == lastWidth && Screen.height == lastHeight)
        {
            return;
        }

        isPortraitLayout = usePortrait;
        lastWidth = Screen.width;
        lastHeight = Screen.height;

        ApplyCameraScale();
        ApplyUiScale();
    }

    private bool IsMobileCapabilitySet()
    {
        // Capability-based heuristic:
        // mobile layouts generally expose touch and typically omit hardware keyboard/mouse.
        bool hasTouch = Touchscreen.current != null;
        bool hasKeyboard = Keyboard.current != null;
        bool hasMouse = Mouse.current != null;

        return hasTouch && !hasKeyboard && !hasMouse;
    }

    private void ApplyCameraScale()
    {
        if (targetCamera == null || !targetCamera.orthographic)
        {
            return;
        }

        float currentAspect = Mathf.Max(0.01f, (float)Screen.width / Screen.height);
        float targetAspect = isPortraitLayout ? portraitReferenceAspect : landscapeReferenceAspect;

        // Keep reference world height, but expand size on very narrow displays to preserve gameplay width.
        float baseOrthoSize = referenceWorldHeight * 0.5f;
        float widthCompensation = currentAspect < targetAspect ? targetAspect / currentAspect : 1f;
        targetCamera.orthographicSize = baseOrthoSize * widthCompensation;
    }

    private void ApplyUiScale()
    {
        if (canvasScalers == null)
        {
            return;
        }

        foreach (CanvasScaler scaler in canvasScalers)
        {
            if (scaler == null)
            {
                continue;
            }

            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = isPortraitLayout ? portraitReferenceResolution : landscapeReferenceResolution;
            scaler.matchWidthOrHeight = isPortraitLayout ? 1f : 0f;
        }
    }
}
