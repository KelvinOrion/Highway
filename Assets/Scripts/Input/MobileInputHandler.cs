using UnityEngine;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

public class MobileInputHandler : MonoBehaviour, IPlayerInput
{
    public event System.Action OnMoveForward;
    public event System.Action OnMoveBack;
    public event System.Action OnMoveLeft;
    public event System.Action OnMoveRight;

    private const float SwipeThreshold = 50f;
    private Vector2 touchStart;

    private void OnEnable() => EnhancedTouchSupport.Enable();

    private void OnDisable() => EnhancedTouchSupport.Disable();

    private void Update()
    {
        foreach (Touch touch in Touch.activeTouches)
        {
            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Began)
            {
                touchStart = touch.screenPosition;
            }

            if (touch.phase == UnityEngine.InputSystem.TouchPhase.Ended)
            {
                EvaluateSwipe(touch.screenPosition);
            }
        }
    }

    private void EvaluateSwipe(Vector2 end)
    {
        Vector2 delta = end - touchStart;
        switch (InputDirectionUtility.GetSwipeDirection(delta, SwipeThreshold))
        {
            case InputDirectionUtility.SwipeDirection.Forward:
                OnMoveForward?.Invoke();
                break;
            case InputDirectionUtility.SwipeDirection.Back:
                OnMoveBack?.Invoke();
                break;
            case InputDirectionUtility.SwipeDirection.Left:
                OnMoveLeft?.Invoke();
                break;
            case InputDirectionUtility.SwipeDirection.Right:
                OnMoveRight?.Invoke();
                break;
        }
    }
}
