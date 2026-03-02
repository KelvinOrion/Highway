using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum DeathType
{
    VehicleCollision,
    BacktrackLimit
}

public class GameManager : MonoBehaviour
{
    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;
    [SerializeField] private Transform terrainHolder;
    [SerializeField] private TMPro.TextMeshProUGUI scoreLabel;
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;
    [SerializeField] private TMPro.TextMeshProUGUI finalScore;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;
    [SerializeField] private Road roadPrefab;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.1f;
    [SerializeField] private int spawnDistance = 25;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.15f;
    [SerializeField] float deadZoneLeft = -0.5f;
    [SerializeField] float deadZoneRight = 0.5f;
    [SerializeField] float minCamOffset = -3f;
    [SerializeField] float maxCamOffset = 6f;
    [SerializeField] float forwardFollowStrength = 0.15f;
    [SerializeField] float maxForwardOffset = 3f;
    [SerializeField] Vector3 cameraOffset = new Vector3(3f, 9f, -5f);

    enum GameState
    {
        Playing,
        Dead,
        Restarting
    }

    private enum InputAction
    {
        None,
        Restart,
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight
    }

    private GameState gameState;
    private Vector2Int characterPos;
    private int spawnLocation;
    private List<(float terrainHeight, HashSet<int> locations, GameObject obj)> obstacles = new();
    private int score = 0;
    private Vector3 cameraBasePos;
    private Vector2 touchStartPos;
    private bool isTouching;
    private bool isMoving;
    private bool hasDied;
    private bool waitForTouchRelease;
    private float swipeThreshold = 60f;
    private float currentForwardOffset = 0f;
    private float fixedCameraY;
    private Character characterController;
    public GameObject deathUI;

    void Awake()
    {
        characterController = character.GetComponent<Character>();
        NewLevel();
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        fixedCameraY = character.position.y + cameraOffset.y;
    }

    private void NewLevel()
    {
        gameState = GameState.Restarting;
        hasDied = false;
        waitForTouchRelease = true;
        isTouching = false;
        isMoving = false;

        if (deathUI != null)
            deathUI.SetActive(false);

        if (scoreLabel != null)
            scoreLabel.gameObject.SetActive(true);
        if (scoreText != null)
            scoreText.gameObject.SetActive(true);

        characterPos = new Vector2Int(0, -3);
        character.position = new Vector3(0, 0.2f, -3);
        characterController.ResetCharacter();
        if (characterModel != null)
        {
            characterModel.gameObject.SetActive(true);
        }

        score = 0;
        scoreText.text = "0";

        obstacles.Clear();
        foreach (Transform child in terrainHolder)
        {
            Destroy(child.gameObject);
        }

        spawnLocation = 0;
        SpawnRoad();

        for (int i = 1; i < spawnDistance; i++)
        {
            SpawnObstacles();
        }

        ResetCameraToPlayer();
        currentForwardOffset = 0f;
    }

    private void SpawnRoad()
    {
        Road road = Instantiate(roadPrefab, terrainHolder);
        obstacles.Add((0.1f, road.Init(spawnLocation), road.gameObject));
        road.gameObject.name = $"{spawnLocation} - Road (Forced)";
        spawnLocation++;
    }

    private void SpawnObstacles()
    {
        float roadProbability = Mathf.Lerp(0.3f, 0.5f, spawnLocation / 250f);

        if (Random.value < roadProbability)
        {
            Road road = Instantiate(roadPrefab, terrainHolder);
            obstacles.Add((0.1f, road.Init(spawnLocation), road.gameObject));
            road.gameObject.name = $"{spawnLocation} - Road";
        }
        else
        {
            Grass grass = Instantiate(grassPrefab, terrainHolder);
            obstacles.Add((0.2f, grass.Init(spawnLocation), grass.gameObject));
            grass.gameObject.name = $"{spawnLocation} - Grass";
        }

        spawnLocation++;
    }

    private bool InStartArea(Vector2Int location)
    {
        if ((location.y > -5) && (location.y < 0) && (location.x > -10) && (location.x < 10)) { return true; }
        return false;
    }

    private IEnumerator MoveCharacter()
    {
        isMoving = true;
        float elapsedTime = 0f;

        float yHeight = 0.2f;
        if (characterPos.y >= 0 && characterPos.y < obstacles.Count)
        {
            yHeight = obstacles[characterPos.y].terrainHeight;
        }

        Vector3 startPos = character.position;
        Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

        while (elapsedTime < moveDuration)
        {
            float percent = elapsedTime / moveDuration;
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);

            float arcHeight = 0.5f * Mathf.Sin(Mathf.PI * percent);
            newPos.y = yHeight + arcHeight;
            character.position = newPos;

            elapsedTime += Time.deltaTime;
            yield return null;
        }

        character.position = endPos;
        isMoving = false;
    }

    private void TryMove(Vector2Int direction)
    {
        if (direction == Vector2Int.zero) return;
        if (gameState != GameState.Playing || isMoving || hasDied) return;

        Vector2Int destination = characterPos + direction;

        if (InStartArea(destination) || ((destination.y >= 0) && (destination.y < obstacles.Count) && !obstacles[destination.y].locations.Contains(destination.x)))
        {
            characterPos = destination;
            StartCoroutine(MoveCharacter());

            if (direction == Vector2Int.up)
            {
                currentForwardOffset = Mathf.Min(
                    currentForwardOffset + forwardFollowStrength,
                    maxForwardOffset
                );
            }

            if ((destination.y + 1) > score)
            {
                score = destination.y + 1;
                scoreText.text = $"{score}";
            }

            while (obstacles.Count < (characterPos.y + spawnDistance))
            {
                SpawnObstacles();

                int oldIndex = characterPos.y - spawnDistance;
                if ((oldIndex >= 0) && (oldIndex < obstacles.Count) && (obstacles[oldIndex].obj != null))
                {
                    Destroy(obstacles[oldIndex].obj);
                }
            }

            if (characterPos.y < (score - 10))
            {
                Die(DeathType.BacktrackLimit);
            }
        }
    }

    void Update()
    {
        HandleTouchInput();
    }

    void LateUpdate()
    {
        if (gameState == GameState.Dead)
            return;

        Camera cam = Camera.main;

        Vector3 camRight = cam.transform.right;
        Vector3 camForward = cam.transform.forward;
        Vector3 camUp = cam.transform.up;

        Vector3 targetPos = character.position + cameraOffset;
        targetPos.y = fixedCameraY;

        float characterRight = Vector3.Dot(character.position, camRight);
        float cameraRight = Vector3.Dot(cameraBasePos, camRight);

        float deltaRight = characterRight - cameraRight;
        if (deltaRight < deadZoneLeft)
            cameraRight = characterRight - deadZoneLeft;
        else if (deltaRight > deadZoneRight)
            cameraRight = characterRight - deadZoneRight;

        cameraRight = Mathf.Clamp(cameraRight, characterRight + minCamOffset, characterRight + maxCamOffset);

        targetPos =
            camRight * cameraRight +
            camForward * Vector3.Dot(targetPos, camForward) +
            camUp * Vector3.Dot(targetPos, camUp);

        targetPos.z = character.position.z + cameraOffset.z;

        cam.transform.position = targetPos;
        cameraBasePos = targetPos;
    }

    private void HandleTouchInput()
    {
        InputAction action = DetectTouchAction();
        ProcessInputAction(action);
    }

    private InputAction DetectTouchAction()
    {
        if (!Application.isMobilePlatform)
            return InputAction.None;

        if (waitForTouchRelease)
        {
            if (Input.touchCount == 0)
            {
                waitForTouchRelease = false;
            }
            return InputAction.None;
        }

        if (Input.touchCount == 0)
            return InputAction.None;

        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            isTouching = true;
            touchStartPos = touch.position;
            return InputAction.None;
        }

        if (touch.phase != TouchPhase.Ended || !isTouching)
            return InputAction.None;

        isTouching = false;

        if (gameState == GameState.Dead)
        {
            return InputAction.Restart;
        }

        Vector2 delta = touch.position - touchStartPos;

        if (delta.magnitude < swipeThreshold)
        {
            return InputAction.MoveUp;
        }

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x > 0 ? InputAction.MoveRight : InputAction.MoveLeft;
        }

        return delta.y > 0 ? InputAction.MoveUp : InputAction.MoveDown;
    }

    private void ProcessInputAction(InputAction action)
    {
        switch (action)
        {
            case InputAction.Restart:
                NewLevel();
                return;
            case InputAction.MoveUp:
                TryMove(Vector2Int.up);
                return;
            case InputAction.MoveDown:
                TryMove(Vector2Int.down);
                return;
            case InputAction.MoveLeft:
                TryMove(Vector2Int.left);
                return;
            case InputAction.MoveRight:
                TryMove(Vector2Int.right);
                return;
            default:
                return;
        }
    }

    public void Die(DeathType type)
    {
        if (hasDied)
            return;

        hasDied = true;
        gameState = GameState.Dead;
        isMoving = false;

        Vector3 deathPoint = character.position + new Vector3(0, 0.2f, 0.5f);
        if (type == DeathType.VehicleCollision)
        {
            deathPoint = character.position;
        }

        characterController.PlayDeathFeedback(deathPoint);
        if (characterModel != null)
        {
            characterModel.gameObject.SetActive(false);
        }

        StartCoroutine(ScreenShake());

        if (scoreLabel != null)
            scoreLabel.gameObject.SetActive(false);
        if (scoreText != null)
            scoreText.gameObject.SetActive(false);

        if (deathUI != null)
        {
            finalScore.text = scoreText.text;
            deathUI.SetActive(true);
        }
    }

    void ResetCameraToPlayer()
    {
        Camera cam = Camera.main;
        Vector3 camPos = cam.transform.position;

        Vector3 camRight = cam.transform.right;
        float playerOffset = Vector3.Dot(character.position, camRight);

        camPos += camRight * (playerOffset - Vector3.Dot(camPos, camRight));

        cam.transform.position = camPos;
        cameraBasePos = camPos;

        gameState = GameState.Playing;
    }

    private IEnumerator ScreenShake()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            Vector3 offset = Random.insideUnitSphere * shakeMagnitude;
            Camera.main.transform.position = cameraBasePos + new Vector3(offset.x, offset.y, 0);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Camera.main.transform.position = cameraBasePos;
    }
}
