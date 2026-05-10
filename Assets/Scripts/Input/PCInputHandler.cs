using UnityEngine;
using UnityEngine.InputSystem;

public class PCInputHandler : MonoBehaviour, IPlayerInput
{
    public event System.Action OnMoveForward;
    public event System.Action OnMoveBack;
    public event System.Action OnMoveLeft;
    public event System.Action OnMoveRight;

    private const float SwipeThreshold = 60f;

    private InputAction moveForward;
    private InputAction moveBack;
    private InputAction moveLeft;
    private InputAction moveRight;
    private InputAction click;
    private InputAction pointerPosition;
    private InputAction touchPosition;
    private Vector2 mouseStart;
    private bool isMouseDragging;
    private bool dragStartedFromTouch;
    private bool actionsInitialized;

    private void Awake()
    {
        EnsureActions();
    }

    private void EnsureActions()
    {
        if (actionsInitialized)
        {
            return;
        }

        moveForward = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/w");
        moveForward.AddBinding("<Keyboard>/upArrow");

        moveBack = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/s");
        moveBack.AddBinding("<Keyboard>/downArrow");

        moveLeft = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/a");
        moveLeft.AddBinding("<Keyboard>/leftArrow");

        moveRight = new InputAction(type: InputActionType.Button, binding: "<Keyboard>/d");
        moveRight.AddBinding("<Keyboard>/rightArrow");

        click = new InputAction(type: InputActionType.Button, binding: "<Mouse>/leftButton");
        click.AddBinding("<Touchscreen>/primaryTouch/press");
        click.AddBinding("<Pen>/tip");

        pointerPosition = new InputAction(type: InputActionType.Value, binding: "<Pointer>/position");
        touchPosition = new InputAction(type: InputActionType.Value, binding: "<Touchscreen>/primaryTouch/position");

        moveForward.performed += _ => OnMoveForward?.Invoke();
        moveBack.performed += _ => OnMoveBack?.Invoke();
        moveLeft.performed += _ => OnMoveLeft?.Invoke();
        moveRight.performed += _ => OnMoveRight?.Invoke();
        click.started += BeginMouseDrag;
        click.canceled += _ => EndMouseDrag();

        actionsInitialized = true;
    }

    private void OnEnable()
    {
        EnsureActions();

        moveForward.Enable();
        moveBack.Enable();
        moveLeft.Enable();
        moveRight.Enable();
        click.Enable();
        pointerPosition.Enable();
        touchPosition.Enable();
    }

    private void OnDisable()
    {
        if (!actionsInitialized)
        {
            return;
        }

        moveForward.Disable();
        moveBack.Disable();
        moveLeft.Disable();
        moveRight.Disable();
        click.Disable();
        pointerPosition.Disable();
        touchPosition.Disable();
    }

    private void OnDestroy()
    {
        if (!actionsInitialized)
        {
            return;
        }

        moveForward.Dispose();
        moveBack.Dispose();
        moveLeft.Dispose();
        moveRight.Dispose();
        click.Dispose();
        pointerPosition.Dispose();
        touchPosition.Dispose();
    }

    private void BeginMouseDrag(InputAction.CallbackContext context)
    {
        isMouseDragging = true;
        dragStartedFromTouch = context.control?.device is Touchscreen;
        mouseStart = ReadPointerPosition();
    }

    private void EndMouseDrag()
    {
        if (!isMouseDragging)
        {
            return;
        }

        Vector2 delta = ReadPointerPosition() - mouseStart;
        isMouseDragging = false;
        dragStartedFromTouch = false;
        EvaluateSwipe(delta);
    }

    private Vector2 ReadPointerPosition()
    {
        return dragStartedFromTouch
            ? touchPosition.ReadValue<Vector2>()
            : pointerPosition.ReadValue<Vector2>();
    }

    private void EvaluateSwipe(Vector2 delta)
    {
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
