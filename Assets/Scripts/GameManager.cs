using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;
    [SerializeField] private Transform terrainHolder;
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;
    [SerializeField] private AdaptiveInputRouter inputRouter;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;
    [SerializeField] private Road roadPrefab;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.1f;
    [SerializeField] private int spawnDistance = 20;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.15f;
    [SerializeField] float deadZoneLeft = -0.5f;
    [SerializeField] float deadZoneRight = 0.5f;
    [SerializeField] float minCamOffset = -3f;
    [SerializeField] float maxCamOffset = 6f;
    [SerializeField] float forwardFollowStrength = 0.15f;
    [SerializeField] float maxForwardOffset = 3f;
    [SerializeField] Vector3 cameraOffset = new Vector3(3f, 8f, -5f);

    enum GameState
    {
        Ready,
        Moving,
        Dead
    }

    private GameState gameState;
    private Vector2Int characterPos;
    private int spawnLocation;
    private readonly List<(float terrainHeight, HashSet<int> locations, GameObject obj)> obstacles = new();
    private int score = 0;
    private Vector3 cameraBasePos;
    float inputLockTimer = 1f;
    float inputLockDuration = 0.4f;
    float currentForwardOffset = 0f;
    float fixedCameraY;
    public GameObject deathUI;

    void Awake()
    {
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
        gameState = GameState.Ready;

        if (deathUI != null)
            deathUI.SetActive(false);

        characterPos = new Vector2Int(0, -1);
        character.position = new Vector3(0, 0.2f, -1);
        character.GetComponent<Character>().Reset();
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
        for (int i = 0; i < spawnDistance; i++)
        {
            SpawnObstacles();
        }

        ResetCameraToPlayer();
        currentForwardOffset = 0f;
    }

    private void SpawnObstacles()
    {
        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

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
        if ((location.y > -5) && (location.y < 0) && (location.x > -6) && (location.x < 6)) { return true; }
        return false;
    }

    private IEnumerator MoveCharacter()
    {
        gameState = GameState.Moving;
        float elapsedTime = 0f;

        float yHeight = 0.2f;
        if (characterPos.y >= 0)
        {
            yHeight = obstacles[characterPos.y].terrainHeight;
        }

        Vector3 startPos = character.position;
        Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

        while (elapsedTime < moveDuration)
        {
            float percent = elapsedTime / moveDuration;

            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);
            newPos.y = yHeight = (0.5f * Mathf.Sin(Mathf.PI * percent));
            character.position = newPos;

            elapsedTime += Time.deltaTime;

            yield return null;
        }

        character.position = endPos;

        if (gameState == GameState.Moving)
        {
            gameState = GameState.Ready;
        }
    }

    private void TryMove(Vector2Int direction)
    {
        if (direction == Vector2Int.zero) return;
        if (gameState != GameState.Ready) return;

        Vector2Int destination = characterPos + direction;

        if (InStartArea(destination) || ((destination.y >= 0) && !obstacles[destination.y].locations.Contains(destination.x)))
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
                if ((oldIndex >= 0) && (obstacles[oldIndex].obj != null))
                {
                    Destroy(obstacles[oldIndex].obj);
                }
            }

            if (characterPos.y < (score - 10))
            {
                character.GetComponent<Character>().Kill(character.position + new Vector3(0, 0.2f, 0.5f));
            }
        }
    }

    void Update()
    {
        if (inputLockTimer > 0f)
        {
            inputLockTimer -= Time.deltaTime;
            return;
        }

        if (gameState == GameState.Dead)
        {
            if (inputRouter != null && inputRouter.RestartRequested())
            {
                NewLevel();
            }
            return;
        }

        if (inputRouter != null && inputRouter.TryGetMove(out Vector2Int moveDirection))
        {
            FaceMoveDirection(moveDirection);
            TryMove(moveDirection);
        }
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

    private void FaceMoveDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.up)
        {
            character.localRotation = Quaternion.identity;
        }
        else if (direction == Vector2Int.down)
        {
            character.localRotation = Quaternion.Euler(0, 180, 0);
        }
        else if (direction == Vector2Int.left)
        {
            character.localRotation = Quaternion.Euler(0, -90, 0);
        }
        else if (direction == Vector2Int.right)
        {
            character.localRotation = Quaternion.Euler(0, 90, 0);
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
    }

    public void PlayerCollision()
    {
        gameState = GameState.Dead;
        StartCoroutine(ScreenShake());
        characterModel.gameObject.SetActive(false);
        inputLockTimer = inputLockDuration;

        if (deathUI != null)
            deathUI.SetActive(true);
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
