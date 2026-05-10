using UnityEngine;

[RequireComponent(typeof(MobileInputHandler))]
[RequireComponent(typeof(PCInputHandler))]
public class InputRouter : MonoBehaviour
{
    [SerializeField] private GameManager gameManager;

    private MobileInputHandler mobileInput;
    private PCInputHandler pcInput;
    private int lastMoveFrame = -1;
    private Vector2Int lastMoveDirection;

    private void Awake()
    {
        if (!ResolveGameManager())
        {
            Debug.LogError($"{nameof(InputRouter)} could not find a {nameof(GameManager)} to route movement input.", this);
            enabled = false;
            return;
        }

        mobileInput = GetComponent<MobileInputHandler>();
        pcInput = GetComponent<PCInputHandler>();

        mobileInput.enabled = false;
        pcInput.enabled = false;

#if UNITY_ANDROID || UNITY_IOS
        mobileInput.enabled = true;
#elif UNITY_WEBGL
        mobileInput.enabled = true;
        pcInput.enabled = true;
#else
        pcInput.enabled = true;
#endif

        BindEvents(mobileInput);
        BindEvents(pcInput);
    }

    private void Reset()
    {
        ResolveGameManager();
    }

    private void OnValidate()
    {
        if (gameManager == null)
        {
            TryGetComponent(out gameManager);
        }
    }

    private void BindEvents(IPlayerInput input)
    {
        input.OnMoveForward += () => RouteMove(Vector2Int.up);
        input.OnMoveBack += () => RouteMove(Vector2Int.down);
        input.OnMoveLeft += () => RouteMove(Vector2Int.left);
        input.OnMoveRight += () => RouteMove(Vector2Int.right);
    }

    private void RouteMove(Vector2Int direction)
    {
        if (Time.frameCount == lastMoveFrame && direction == lastMoveDirection)
        {
            return;
        }

        lastMoveFrame = Time.frameCount;
        lastMoveDirection = direction;
        gameManager.HandleMove(direction);
    }

    private bool ResolveGameManager()
    {
        if (gameManager != null)
        {
            return true;
        }

        if (TryGetComponent(out gameManager))
        {
            return true;
        }

        gameManager = FindFirstObjectByType<GameManager>();
        return gameManager != null;
    }
}
