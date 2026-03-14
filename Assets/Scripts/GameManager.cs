using System;
using System.Collections;
using System.Collections.Generic;
//using UnityEditor.PackageManager;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    [Serializable]
    private class TrafficTier
    {
        [Header("Tier activation")]
        [SerializeField] public int minRow = 0;

        [Header("Road frequency")]
        [Range(0f, 1f)]
        [SerializeField] public float roadProbability = 0.3f;

        [Header("Vehicle pacing")]
        [SerializeField] public float minSpeed = 1f;
        [SerializeField] public float maxSpeed = 3f;
        [SerializeField] public int minVehicleCount = 1;
        [SerializeField] public int maxVehicleCount = 3;
        [SerializeField] public float targetReactionTime = 2f;
        [SerializeField] public float reactionTimeJitter = 0.2f;
        [SerializeField] public float guaranteedSafeWindow = 1f;
    }

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

    [Header("Traffic tuning")]
    [SerializeField] private List<TrafficTier> trafficTiers = new()
    {
        new TrafficTier
        {
            minRow = 0,
            roadProbability = 0.30f,
            minSpeed = 1f,
            maxSpeed = 2.75f,
            minVehicleCount = 1,
            maxVehicleCount = 2,
            targetReactionTime = 2f,
            reactionTimeJitter = 0.2f,
            guaranteedSafeWindow = 1.3f
        },
        new TrafficTier
        {
            minRow = 40,
            roadProbability = 0.38f,
            minSpeed = 2f,
            maxSpeed = 4.75f,
            minVehicleCount = 1,
            maxVehicleCount = 3,
            targetReactionTime = 1f,
            reactionTimeJitter = 0.15f,
            guaranteedSafeWindow = 0.95f
        },
        new TrafficTier
        {
            minRow = 90,
            roadProbability = 0.5f,
            minSpeed = 3.25f,
            maxSpeed = 6f,
            minVehicleCount = 2,
            maxVehicleCount = 3,
            targetReactionTime = 0.8f,
            reactionTimeJitter = 0.12f,
            guaranteedSafeWindow = 0.75f
        }
    };
    [SerializeField] private float trafficWrapX = 15f;
    [SerializeField] private int leftEdgeThreshold = -4;
    [Range(0f, 0.4f)]
    [SerializeField] private float leftEdgeDirectionBias = 0.16f;


    //6 references
    enum GameState
    {
        Ready,
        Moving,
        Dead
    }
    private GameState gameState;
    private Vector2Int characterPos;
    private int spawnLocation;
    private List<(float terrainHeight, HashSet<int> locations, GameObject obj)> obstacles = new();
    private int score = 0;
    private Vector3 cameraBasePos;
    Vector2 touchStartPos;
    bool isTouching;
    float swipeThreshold = 60f; // tune the swipe threshold
    float inputLockTimer = 1f;
    float inputLockDuration = 0.8f; // 200ms feels right on mobile
    float currentForwardOffset = 0f;
    float fixedCameraY;
    public GameObject deathUI;
    //remove hold-based movement
    //bool isHoldingForward = false;
    //float forwardHoldTimer = 0f;
    //float forwardStepInterval = 0.18f; // default speed

    void Awake()
    {
        EnsureTrafficTierDefaults();
        trafficTiers.Sort((a, b) => a.minRow.CompareTo(b.minRow));
        //Initialise all the starting state/
        NewLevel();
    }

    private void OnValidate()
    {
        EnsureTrafficTierDefaults();
        trafficTiers.Sort((a, b) => a.minRow.CompareTo(b.minRow));
    }

    private void EnsureTrafficTierDefaults()
    {
        if ((trafficTiers == null) || (trafficTiers.Count == 0))
        {
            trafficTiers = new List<TrafficTier>
            {
                new TrafficTier()
            };
        }
    }

    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;

        Camera cam = Camera.main;

        // Intention-based camera setup
        fixedCameraY = character.position.y + cameraOffset.y;
    }

    private void NewLevel()
    {
        gameState = GameState.Ready;
        
        // HIDE DEATH SCREEN(prevents persistent display)
        if (deathUI != null)
            deathUI.SetActive(false); // Critical: cleans UI state

        // SHOW SCORE UI when new level starts
        if (scoreLabel != null)
            scoreLabel.gameObject.SetActive(true);
        if (scoreText != null)
            scoreText.gameObject.SetActive(true);

        // Reset character position every new round
        // Keep characterPos and character.position in sync: both should represent the same logical position
        characterPos = new Vector2Int(0, -3);
        character.position = new Vector3(0, 0.2f, -3);
        character.GetComponent<Character>().Reset();
        if (characterModel != null)
        {
            characterModel.gameObject.SetActive(true);
        }

        // Reset score 
        score = 0;
        scoreText.text = "0";
        //Remove all terrain
        obstacles.Clear();
        foreach (Transform child in terrainHolder)
        {
            Destroy(child.gameObject);
        }

        // Spawn terrain ahead of the character
        // First tile (spawnLocation = 0) is always a road to ensure smooth gameplay transition
        spawnLocation = 0;
        SpawnRoad(); // Force first tile to be a road with proper car mechanics
        
        // Spawn remaining tiles with random terrain
        for (int i = 1; i < spawnDistance; i++)
        {
            SpawnObstacles();
        }

        //Reset camera after player respawn
        ResetCameraToPlayer();
        currentForwardOffset = 0f;

        // Lock input after restarting to prevent double-tap forward bug
        //inputLockTimer = restartInputLockDuration;
    }

    private void SpawnRoad()
    {
        // Force spawn a road at the current spawnLocation
        Road road = Instantiate(roadPrefab, terrainHolder);
        TrafficTier tier = GetTrafficTier(spawnLocation);
        obstacles.Add((0.1f, road.Init(spawnLocation, BuildRoadConfig(tier)), road.gameObject));
        road.gameObject.name = $"{spawnLocation} - Road (Forced)";
        
        //Update to the next location
        spawnLocation++;
    }

    private void SpawnObstacles()
    {
        TrafficTier tier = GetTrafficTier(spawnLocation);
        float roadProbability = tier.roadProbability;

        if (UnityEngine.Random.value < roadProbability)
        {
            // Create road with terrain height of 0.1f
            Road road = Instantiate(roadPrefab, terrainHolder);
            obstacles.Add((0.1f, road.Init(spawnLocation, BuildRoadConfig(tier)), road.gameObject));
            road.gameObject.name = $"{spawnLocation} - Road";
        }
        else
        {
            // Create grass with terrain height of 0.2f
            Grass grass = Instantiate(grassPrefab, terrainHolder);
            obstacles.Add((0.2f, grass.Init(spawnLocation), grass.gameObject));
            grass.gameObject.name = $"{spawnLocation} - Grass";
        }

        // Update to the next location
        spawnLocation++;
    }

    private TrafficTier GetTrafficTier(int row)
    {
        TrafficTier selected = trafficTiers[0];
        foreach (TrafficTier tier in trafficTiers)
        {
            if (row >= tier.minRow)
            {
                selected = tier;
            }
            else
            {
                break;
            }
        }

        return selected;
    }

    private Road.SpawnConfig BuildRoadConfig(TrafficTier tier)
    {
        float positiveDirectionChance = 0.5f;
        if (characterPos.x <= leftEdgeThreshold)
        {
            positiveDirectionChance += leftEdgeDirectionBias;
        }

        int minVehicleCount = Mathf.Max(0, tier.minVehicleCount);
        int maxVehicleCount = Mathf.Max(minVehicleCount, tier.maxVehicleCount);
        float minSpeed = Mathf.Max(0.5f, Mathf.Min(tier.minSpeed, tier.maxSpeed));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(tier.minSpeed, tier.maxSpeed));

        return new Road.SpawnConfig
        {
            minSpeed = minSpeed,
            maxSpeed = maxSpeed,
            minVehicleCount = minVehicleCount,
            maxVehicleCount = maxVehicleCount,
            targetReactionTime = tier.targetReactionTime,
            reactionTimeJitter = tier.reactionTimeJitter,
            minGuaranteedSafeWindow = tier.guaranteedSafeWindow,
            positiveDirectionChance = Mathf.Clamp01(positiveDirectionChance),
            wrapX = trafficWrapX
        };
    }

    private bool InStartArea(Vector2Int location)
    {
        //Movement anywhere in the starting region is aligned.
        if ((location.y > -5) && (location.y < 0) && (location.x > -10) && (location.x < 10)) { return true; }
        return false;
    }

    private IEnumerator MoveCharacter()
    {
        gameState = GameState.Moving;
        float elapsedTime = 0f;

        //The yHeight changes if we're on grass or road
        float yHeight = 0.2f;
        if (characterPos.y >= 0 && characterPos.y < obstacles.Count)
        {
            yHeight = obstacles[characterPos.y].terrainHeight;
        }

        Vector3 startPos = character.position;
        Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

        while (elapsedTime < moveDuration)
        {
            //How far through the animation are we
            float percent = elapsedTime / moveDuration;

            //Update character position
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);

            //Make character jump in an arc
            float arcHeight = 0.5f * Mathf.Sin(Mathf.PI * percent);
            newPos.y = yHeight + arcHeight;
            character.position = newPos;

            //Update elapsed time
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        //Ensure we're at the end
        character.position = endPos;

        //Need to check we're still in moving at the end.
        //If we're dead we don't want to go back to ready.
        if (gameState == GameState.Moving)
        {
            gameState = GameState.Ready;
        }
    }

    // function to move character, and build for independent from platform
    private void TryMove(Vector2Int direction)
    {
        // Ignore empty intent
        if (direction == Vector2Int.zero) return;

        // Only move when ready
        if (gameState != GameState.Ready) return;

        Vector2Int destination = characterPos + direction;

        // move area
        if (InStartArea(destination) || ((destination.y >= 0) && (destination.y < obstacles.Count) && !obstacles[destination.y].locations.Contains(destination.x)))
        {
            {
                characterPos = destination;
                StartCoroutine(MoveCharacter());
                //Move camera forwards
                if (direction == Vector2Int.up)
                {
                    currentForwardOffset = Mathf.Min(
                        currentForwardOffset + forwardFollowStrength,
                        maxForwardOffset
                    );
                }

                // Update score if we moved forward
                if ((destination.y + 1) > score)
                {
                    score = destination.y + 1;
                    scoreText.text = $"{score}";
                }
            }

            // Spawn new obstacles if necessary
            while (obstacles.Count < (characterPos.y + spawnDistance))
            {
                SpawnObstacles();

                // Destroy old obstacles to save memory
                int oldIndex = characterPos.y - spawnDistance;
                if ((oldIndex >= 0) && (oldIndex < obstacles.Count) && (obstacles[oldIndex].obj != null))
                {
                    Destroy(obstacles[oldIndex].obj);
                }
            }

            // If character gone too far back, end game
            if (characterPos.y < (score - 10))
            {
                character.GetComponent<Character>().Kill(character.position + new Vector3(0, 0.2f, 0.5f));
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        //if (inputLockTimer > 0f)
        //{
        //    inputLockTimer -= Time.deltaTime;
        //    return; // Ignore all input
        //}

        HandleTouchInput();

        // (Mobile) Can only use shortcut to restart when dead
        if (gameState == GameState.Dead && Input.touchCount > 0)
        {
            NewLevel();
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

        // Only move the camera when the character crosses the dead-zone.
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

    //----------------Function---------------
    void HandleTouchInput()
    {
        if (!Application.isMobilePlatform) return;

        if (Input.touchCount == 0) return;

        Touch touch = Input.GetTouch(0);

        switch (touch.phase)
        {
            case UnityEngine.TouchPhase.Began:
                isTouching = true;
                touchStartPos = touch.position;
                break;

            case UnityEngine.TouchPhase.Ended:
                if (!isTouching) return;

                Vector2 delta = touch.position - touchStartPos;
                isTouching = false;

                // Small movement = tap → forward
                if (delta.magnitude < swipeThreshold)
                {
                    TryMove(Vector2Int.up);
                    return;
                }

                // Horizontal swipe
                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    TryMove(delta.x > 0 ? Vector2Int.right : Vector2Int.left);
                }
                // Vertical swipe
                else
                {
                    if (delta.y > 0)
                        TryMove(Vector2Int.up);
                    else
                        TryMove(Vector2Int.down);
                }

                break;
        }
    }

    void ResetCameraToPlayer()
    {
        Camera cam = Camera.main;
        Vector3 camPos = cam.transform.position;

        // Keep current height & depth, reset horizontal framing
        Vector3 camRight = cam.transform.right;
        float playerOffset = Vector3.Dot(character.position, camRight);

        camPos += camRight * (playerOffset - Vector3.Dot(camPos, camRight));

        cam.transform.position = camPos;
        cameraBasePos = camPos;
    }

    public void PlayerCollision()
    {
        // Set game state to dead
        gameState = GameState.Dead;
        StartCoroutine(ScreenShake());
        // Disable character model
        characterModel.gameObject.SetActive(false);
        inputLockTimer = inputLockDuration;

        // HIDE SCORE UI when player dies
        if (scoreLabel != null)
            scoreLabel.gameObject.SetActive(false);
        if (scoreText != null)
            scoreText.gameObject.SetActive(false);

        // Update final score display and show death screen
        if (deathUI != null)
        {
            finalScore.text = scoreText.text; // Update final score display
            deathUI.SetActive(true); // Critical: makes death visible
        }
    }

    private IEnumerator ScreenShake()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            Vector3 offset = UnityEngine.Random.insideUnitSphere * shakeMagnitude;
            Camera.main.transform.position = cameraBasePos + new Vector3(offset.x, offset.y, 0);

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Camera.main.transform.position = cameraBasePos;
    }
}
