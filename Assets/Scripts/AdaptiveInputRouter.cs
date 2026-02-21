using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Provides a single gameplay input API by selecting the most suitable device source at runtime.
/// Architecture note:
/// - We avoid platform checks and instead inspect active device capabilities.
/// - Keyboard has priority when present, matching desktop/web expectations.
/// - Touch fallback enables swipe/tap input when a touchscreen is the available input path.
/// </summary>
public class AdaptiveInputRouter : MonoBehaviour
{
    [Header("Swipe tuning")]
    [SerializeField] private float swipeThresholdPixels = 60f;

    private Vector2 touchStartPos;
    private bool isTouchActive;

    public enum InputMode
    {
        Keyboard,
        Touch
    }

    public InputMode CurrentMode { get; private set; } = InputMode.Keyboard;

    private void Awake()
    {
        ResolveMode();
    }

    private void OnEnable()
    {
        InputSystem.onDeviceChange += OnDeviceChange;
        ResolveMode();
    }

    private void OnDisable()
    {
        InputSystem.onDeviceChange -= OnDeviceChange;
    }

    private void OnDeviceChange(InputDevice _, InputDeviceChange __)
    {
        ResolveMode();
    }

    private void ResolveMode()
    {
        // Capability-first detection: prefer keyboard when one exists.
        if (Keyboard.current != null)
        {
            CurrentMode = InputMode.Keyboard;
            return;
        }

        if (Touchscreen.current != null)
        {
            CurrentMode = InputMode.Touch;
            return;
        }

        // Safe fallback so gameplay still works with simulated keyboard devices.
        CurrentMode = InputMode.Keyboard;
    }

    public bool TryGetMove(out Vector2Int direction)
    {
        ResolveMode();
        direction = Vector2Int.zero;

        if (CurrentMode == InputMode.Keyboard)
        {
            return TryGetKeyboardMove(out direction);
        }

        return TryGetSwipeMove(out direction);
    }

    public bool RestartRequested()
    {
        ResolveMode();

        if (CurrentMode == InputMode.Keyboard)
        {
            return Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame;
        }

        if (Touchscreen.current == null)
        {
            return false;
        }

        var touch = Touchscreen.current.primaryTouch;
        return touch.phase.ReadValue() == UnityEngine.InputSystem.TouchPhase.Began;
    }

    private bool TryGetKeyboardMove(out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        if (Keyboard.current == null)
        {
            return false;
        }

        if (Keyboard.current.upArrowKey.wasPressedThisFrame || Keyboard.current.wKey.wasPressedThisFrame)
        {
            direction = Vector2Int.up;
        }
        else if (Keyboard.current.downArrowKey.wasPressedThisFrame || Keyboard.current.sKey.wasPressedThisFrame)
        {
            direction = Vector2Int.down;
        }
        else if (Keyboard.current.leftArrowKey.wasPressedThisFrame || Keyboard.current.aKey.wasPressedThisFrame)
        {
            direction = Vector2Int.left;
        }
        else if (Keyboard.current.rightArrowKey.wasPressedThisFrame || Keyboard.current.dKey.wasPressedThisFrame)
        {
            direction = Vector2Int.right;
        }

        return direction != Vector2Int.zero;
    }

    private bool TryGetSwipeMove(out Vector2Int direction)
    {
        direction = Vector2Int.zero;

        if (Touchscreen.current == null)
        {
            return false;
        }

        var touch = Touchscreen.current.primaryTouch;
        var phase = touch.phase.ReadValue();
        var position = touch.position.ReadValue();

        if (phase == UnityEngine.InputSystem.TouchPhase.Began)
        {
            isTouchActive = true;
            touchStartPos = position;
            return false;
        }

        if (!isTouchActive || phase != UnityEngine.InputSystem.TouchPhase.Ended)
        {
            return false;
        }

        isTouchActive = false;
        Vector2 delta = position - touchStartPos;

        // Small swipe/tap maps to forward, maintaining one-handed mobile usability.
        if (delta.magnitude < swipeThresholdPixels)
        {
            direction = Vector2Int.up;
            return true;
        }

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            direction = delta.x > 0 ? Vector2Int.right : Vector2Int.left;
        }
        else
        {
            direction = delta.y > 0 ? Vector2Int.up : Vector2Int.down;
        }

        return true;
    }
}
