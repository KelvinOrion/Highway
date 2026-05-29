using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(MakController))]
public class GameManager : MonoBehaviour
{
    private const int TargetFrameRate = 60;
    private const int StartX = 0;
    private const int StartY = -3;
    private const int StartAreaMinX = -10;
    private const int StartAreaMaxX = 10;
    private const int StartAreaMinY = -5;
    private const int StartAreaMaxY = 0;
    private const int MaxStepsBehindScore = 10;
    private const float RoadHeight = 0.1f;
    private const float GrassHeight = 0.2f;
    private const float DefaultHopAnimationDuration = 0.3f;
    private const float MinMoveDuration = 0.01f;
    private const float RiverDeathHeightOffset = 0.2f;
    private const float RiverDeathForwardOffset = 0.5f;
    private const int FallbackDeathDataVariantCount = 5;
    private const float DefaultPositiveDirectionChance = 0.5f;
    private const float MinTrafficSpeed = 0.5f;
    private const int MilestoneFontSize = 52;
    private const float MilestoneAnchorX = 0.5f;
    private const float MilestoneAnchorY = 0.72f;
    private const float MilestoneWidth = 520f;
    private const float MilestoneHeight = 110f;
    private const float MakDeathFlashDuration = 0.2f;
    private const float MakDeathFlashAlpha = 0.85f;

    [System.Serializable]
    private sealed class TrafficTier
    {
        [Header("Tier activation")]
        [SerializeField] private int minRow = 0;

        [Header("Road frequency")]
        [Range(0f, 1f)]
        [SerializeField] private float roadProbability = 0.3f;

        [Header("Vehicle pacing")]
        [SerializeField] private float minSpeed = 1f;
        [SerializeField] private float maxSpeed = 3f;
        [SerializeField] private int minVehicleCount = 1;
        [SerializeField] private int maxVehicleCount = 3;
        [SerializeField] private float targetReactionTime = 2f;
        [SerializeField] private float reactionTimeJitter = 0.2f;
        [SerializeField] private float guaranteedSafeWindow = 1f;

        public TrafficTier()
        {
        }

        public TrafficTier(
            int minRow,
            float roadProbability,
            float minSpeed,
            float maxSpeed,
            int minVehicleCount,
            int maxVehicleCount,
            float targetReactionTime,
            float reactionTimeJitter,
            float guaranteedSafeWindow)
        {
            this.minRow = minRow;
            this.roadProbability = roadProbability;
            this.minSpeed = minSpeed;
            this.maxSpeed = maxSpeed;
            this.minVehicleCount = minVehicleCount;
            this.maxVehicleCount = maxVehicleCount;
            this.targetReactionTime = targetReactionTime;
            this.reactionTimeJitter = reactionTimeJitter;
            this.guaranteedSafeWindow = guaranteedSafeWindow;
            Validate();
        }

        public int MinRow => minRow;
        public float RoadProbability => roadProbability;
        public float MinSpeed => minSpeed;
        public float MaxSpeed => maxSpeed;
        public int MinVehicleCount => minVehicleCount;
        public int MaxVehicleCount => maxVehicleCount;
        public float TargetReactionTime => targetReactionTime;
        public float ReactionTimeJitter => reactionTimeJitter;
        public float GuaranteedSafeWindow => guaranteedSafeWindow;

        public void Validate()
        {
            minRow = Mathf.Max(0, minRow);
            roadProbability = Mathf.Clamp01(roadProbability);
            minSpeed = Mathf.Max(MinTrafficSpeed, minSpeed);
            maxSpeed = Mathf.Max(minSpeed, maxSpeed);
            minVehicleCount = Mathf.Max(0, minVehicleCount);
            maxVehicleCount = Mathf.Max(minVehicleCount, maxVehicleCount);
            targetReactionTime = Mathf.Max(0f, targetReactionTime);
            reactionTimeJitter = Mathf.Max(0f, reactionTimeJitter);
            guaranteedSafeWindow = Mathf.Max(0f, guaranteedSafeWindow);
        }
    }

    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;
    [SerializeField] private Transform terrainHolder;
    [SerializeField] private TMPro.TextMeshProUGUI scoreLabel;
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;
    [SerializeField] private DeathScreen deathScreen;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;
    [SerializeField] private Road roadPrefab;

    [Header("Death screen")]
    [SerializeField] private DeathData[] deathPresets;
    [SerializeField] private float deathScreenDelay = 0.9f;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.1f;
    [SerializeField] private int spawnDistance = 25;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.15f;
    [SerializeField] private Vector3 cameraOffset = new(3f, 9f, -5f);

    [Header("Traffic tuning")]
    [SerializeField] private List<TrafficTier> trafficTiers;
    [SerializeField] private float trafficWrapX = 15f;
    [SerializeField] private int leftEdgeThreshold = -4;
    [Range(0f, 0.4f)]
    [SerializeField] private float leftEdgeDirectionBias = 0.16f;

    [Header("Milestone feedback")]
    [SerializeField] private TextMeshProUGUI milestoneText;
    [SerializeField] private int[] milestoneRows = { 50, 100, 200 };
    [SerializeField] private string milestoneFormat = "{0} baris! Gila!";
    [SerializeField] private float milestoneFlashDuration = 1.5f;

    private enum GameState
    {
        Ready,
        Moving,
        Dead
    }

    private readonly struct TerrainRow
    {
        public TerrainRow(float height, HashSet<int> blockedColumns, GameObject instance)
        {
            Height = height;
            BlockedColumns = blockedColumns;
            Instance = instance;
        }

        public float Height { get; }
        public HashSet<int> BlockedColumns { get; }
        public GameObject Instance { get; }
    }

    private readonly List<TerrainRow> terrainRows = new();
    private readonly WaitForFixedUpdate waitForFixedUpdate = new();
    private Character characterController;
    private Rigidbody characterBody;
    private Animator characterAnimator;
    private float hopAnimationDuration = DefaultHopAnimationDuration;
    private float defaultMoveDuration;
    private GameState gameState;
    private Vector2Int characterPos;
    private int score;
    private int coinsCollected;
    private int spawnLocation;
    private float fixedCameraY;
    private Vector3 cameraBasePos;
    private Vector3 cameraShakeOffset;
    private Coroutine deathSequenceRoutine;
    private Coroutine cameraShakeRoutine;
    private Coroutine milestoneRoutine;
    private Coroutine makDeathSequenceRoutine;
    private Image makDeathFlashImage;
    private GameObject makDeathFlashCanvasObject;
    private readonly HashSet<int> shownMilestones = new();
    private MakController makController;

    public float MoveDuration => moveDuration;
    public int PlayerRow => characterPos.y;
    public int PlayerStartColumn => StartX;
    public bool IsPlayerDead => gameState == GameState.Dead;

    private void Awake()
    {
        ValidateTrafficTiers();
        defaultMoveDuration = moveDuration;
        Application.targetFrameRate = TargetFrameRate;
        QualitySettings.vSyncCount = 0;

        if (character != null)
        {
            characterController = character.GetComponent<Character>();
            characterBody = character.GetComponent<Rigidbody>();
            ConfigureCharacterBody();
            fixedCameraY = character.position.y + cameraOffset.y;
        }

        ResolveMakController();
        NewLevel();
    }

    private void Start()
    {
        characterAnimator = character != null ? character.GetComponentInChildren<Animator>() : null;
        CacheHopAnimationDuration();
    }

    private void OnValidate()
    {
        moveDuration = Mathf.Max(MinMoveDuration, moveDuration);
        spawnDistance = Mathf.Max(1, spawnDistance);
        shakeDuration = Mathf.Max(0f, shakeDuration);
        shakeMagnitude = Mathf.Abs(shakeMagnitude);
        deathScreenDelay = Mathf.Max(0f, deathScreenDelay);
        trafficWrapX = Mathf.Max(1f, trafficWrapX);
        leftEdgeDirectionBias = Mathf.Clamp(leftEdgeDirectionBias, 0f, 0.4f);
        milestoneFlashDuration = Mathf.Max(0f, milestoneFlashDuration);
        ValidateTrafficTiers();
    }

    // Resets runtime state and rebuilds the starting terrain rows.
    private void NewLevel()
    {
        Time.timeScale = 1f;

        if (deathSequenceRoutine != null)
        {
            StopCoroutine(deathSequenceRoutine);
            deathSequenceRoutine = null;
        }

        if (makDeathSequenceRoutine != null)
        {
            StopCoroutine(makDeathSequenceRoutine);
            makDeathSequenceRoutine = null;
        }

        ClearMakDeathFlash();
        makController?.ResetForNewGame();
        ResetPowerups();
        gameState = GameState.Ready;
        shownMilestones.Clear();
        HideMilestoneImmediate();

        HideDeathScreenImmediate();
        SetScoreUiVisible(true);
        ResetCharacter();
        ResetScore();
        ClearTerrain();
        SpawnInitialTerrain();
        ResetCameraToPlayer();
    }

    private void ResetPowerups()
    {
        TehTarikPowerup.ResetRuntimeState();
        HandPowerup.ResetRuntimeState();
        PowerupBase.ClearLivePowerups();
        SetMoveDuration(defaultMoveDuration);
    }

    private void ResetCharacter()
    {
        if (character == null)
        {
            Debug.LogError($"{nameof(GameManager)} is missing its character reference.", this);
            return;
        }

        characterPos = new Vector2Int(StartX, StartY);
        SetCharacterPosition(new Vector3(StartX, GrassHeight, StartY));
        characterController?.Reset();

        if (characterModel != null)
        {
            characterModel.gameObject.SetActive(true);
        }
    }

    private void ResetScore()
    {
        score = 0;
        coinsCollected = 0;
        UpdateScoreText();
    }

    private void ClearTerrain()
    {
        terrainRows.Clear();

        if (terrainHolder == null)
        {
            Debug.LogError($"{nameof(GameManager)} is missing its terrain holder reference.", this);
            return;
        }

        foreach (Transform child in terrainHolder)
        {
            Destroy(child.gameObject);
        }
    }

    private void SpawnInitialTerrain()
    {
        spawnLocation = 0;
        if (!SpawnRoad("Road (Start)"))
        {
            return;
        }

        for (int i = 1; i < spawnDistance; i++)
        {
            if (!SpawnTerrainRow())
            {
                return;
            }
        }
    }

    private bool SpawnRoad(string label = "Road")
    {
        if (roadPrefab == null || terrainHolder == null)
        {
            Debug.LogError($"{nameof(GameManager)} cannot spawn roads until Road Prefab and Terrain Holder are assigned.", this);
            return false;
        }

        Road road = Instantiate(roadPrefab, terrainHolder);
        terrainRows.Add(new TerrainRow(RoadHeight, road.Init(spawnLocation, BuildRoadConfig(GetTrafficTier(spawnLocation))), road.gameObject));
        road.gameObject.name = $"{spawnLocation} - {label}";
        spawnLocation++;
        return true;
    }

    private bool SpawnTerrainRow()
    {
        float roadProbability = GetRoadProbability(spawnLocation);

        if (Random.value < roadProbability)
        {
            return SpawnRoad();
        }

        if (grassPrefab == null || terrainHolder == null)
        {
            Debug.LogError($"{nameof(GameManager)} cannot spawn grass until Grass Prefab and Terrain Holder are assigned.", this);
            return false;
        }

        Grass grass = Instantiate(grassPrefab, terrainHolder);
        terrainRows.Add(new TerrainRow(GrassHeight, grass.Init(spawnLocation), grass.gameObject));
        grass.gameObject.name = $"{spawnLocation} - Grass";
        spawnLocation++;
        return true;
    }

    private static bool InStartArea(Vector2Int location)
    {
        return location.y > StartAreaMinY &&
               location.y < StartAreaMaxY &&
               location.x > StartAreaMinX &&
               location.x < StartAreaMaxX;
    }

    private IEnumerator MoveCharacter()
    {
        gameState = GameState.Moving;

        float elapsedTime = 0f;
        float yHeight = GetTerrainHeight(characterPos);
        Vector3 startPos = characterBody != null ? characterBody.position : character.position;
        Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

        while (elapsedTime < moveDuration)
        {
            float percent = moveDuration <= 0f ? 1f : elapsedTime / moveDuration;
            SetCharacterPosition(Vector3.Lerp(startPos, endPos, percent));

            elapsedTime += Time.fixedDeltaTime;
            yield return waitForFixedUpdate;
        }

        SetCharacterPosition(endPos);

        if (gameState == GameState.Moving)
        {
            gameState = GameState.Ready;
        }

        ResetHopAnimationSpeed();
    }

    private float GetTerrainHeight(Vector2Int position)
    {
        return position.y >= 0 && position.y < terrainRows.Count
            ? terrainRows[position.y].Height
            : GrassHeight;
    }

    private void TryMove(Vector2Int direction)
    {
        if (direction == Vector2Int.zero || gameState != GameState.Ready || character == null)
        {
            return;
        }

        Vector2Int destination = characterPos + direction;
        if (!CanMoveTo(destination))
        {
            return;
        }

        characterPos = destination;
        characterController?.FaceDirection(direction);
        if (characterController == null)
        {
            character.localRotation = Quaternion.Euler(0f, DirectionToYaw(direction), 0f);
        }

        if (makController != null && makController.CheckPlayerMoveForCatch(characterPos.y))
        {
            SetCharacterPosition(new Vector3(characterPos.x, GetTerrainHeight(characterPos), characterPos.y));
            return;
        }

        PlayHopAnimation();
        StartCoroutine(MoveCharacter());

        if (destination.y + 1 > score)
        {
            score = destination.y + 1;
            UpdateScoreText();
            TryShowMilestone(score);
        }

        EnsureTerrainAhead();

        if (characterPos.y < score - MaxStepsBehindScore)
        {
            characterController?.Kill(character.position + new Vector3(0f, RiverDeathHeightOffset, RiverDeathForwardOffset));
        }
    }

    private bool CanMoveTo(Vector2Int destination)
    {
        if (InStartArea(destination))
        {
            return true;
        }

        return destination.y >= 0 &&
               destination.y < terrainRows.Count &&
               !terrainRows[destination.y].BlockedColumns.Contains(destination.x);
    }

    private void EnsureTerrainAhead()
    {
        while (terrainRows.Count < characterPos.y + spawnDistance)
        {
            if (!SpawnTerrainRow())
            {
                return;
            }

            int oldIndex = characterPos.y - spawnDistance;
            if (oldIndex >= 0 && oldIndex < terrainRows.Count && terrainRows[oldIndex].Instance != null)
            {
                Destroy(terrainRows[oldIndex].Instance);
            }
        }
    }

    private void LateUpdate()
    {
        if (character == null)
        {
            return;
        }

        SyncCharacterTransformToBody();

        if (gameState == GameState.Dead)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 targetPos = GetCameraTargetPosition(character.position);
        cameraBasePos = targetPos;
        cam.transform.position = targetPos + cameraShakeOffset;
    }

    // Called by InputRouter. Replaces all legacy Input polling.
    public void HandleMove(Vector2Int direction)
    {
        TryMove(direction);
    }

    private static float DirectionToYaw(Vector2Int direction)
    {
        if (direction == Vector2Int.right) return 90f;
        if (direction == Vector2Int.down) return 180f;
        if (direction == Vector2Int.left) return -90f;

        return 0f;
    }

    private void ResetCameraToPlayer()
    {
        if (cameraShakeRoutine != null)
        {
            StopCoroutine(cameraShakeRoutine);
            cameraShakeRoutine = null;
        }

        cameraShakeOffset = Vector3.zero;

        Camera cam = Camera.main;
        if (cam == null || character == null)
        {
            return;
        }

        fixedCameraY = character.position.y + cameraOffset.y;
        Vector3 camPos = GetCameraTargetPosition(character.position);
        cam.transform.position = camPos;
        cameraBasePos = camPos;
    }

    private Vector3 GetCameraTargetPosition(Vector3 followPosition)
    {
        return new Vector3(
            followPosition.x + cameraOffset.x,
            fixedCameraY,
            followPosition.z + cameraOffset.z);
    }

    public void PlayerCollision()
    {
        if (gameState == GameState.Dead)
        {
            return;
        }

        gameState = GameState.Dead;

        if (deathSequenceRoutine != null)
        {
            StopCoroutine(deathSequenceRoutine);
        }

        deathSequenceRoutine = StartCoroutine(PlayerDeathSequence());
        HideMilestoneImmediate();
    }

    public void PlayerCaughtByMak()
    {
        if (gameState == GameState.Dead)
        {
            return;
        }

        gameState = GameState.Dead;

        if (deathSequenceRoutine != null)
        {
            StopCoroutine(deathSequenceRoutine);
            deathSequenceRoutine = null;
        }

        if (makDeathSequenceRoutine != null)
        {
            StopCoroutine(makDeathSequenceRoutine);
        }

        makDeathSequenceRoutine = StartCoroutine(MakDeathSequence());
        HideMilestoneImmediate();
    }

    private IEnumerator PlayerDeathSequence()
    {
        PlayCameraShake(shakeDuration, shakeMagnitude);

        SetScoreUiVisible(false);

        if (deathScreenDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(deathScreenDelay);
        }

        OnPlayerDeath(ChooseDeathData());
        deathSequenceRoutine = null;
    }

    private IEnumerator MakDeathSequence()
    {
        PlayCameraShake(shakeDuration, shakeMagnitude);
        SetScoreUiVisible(false);

        yield return FlashMakDeathScreen();

        OnPlayerDeath(CreateMakDeathData());
        makDeathSequenceRoutine = null;
    }

    public void AddCoin()
    {
        coinsCollected++;
    }

    public void AddCoins(int amount = 1)
    {
        coinsCollected = Mathf.Max(0, coinsCollected + amount);
    }

    public void SetMoveDuration(float duration)
    {
        moveDuration = Mathf.Max(MinMoveDuration, duration);
    }

    public Vector3 GetCenterColumnWorldPosition(int row)
    {
        return new Vector3(StartX, GetTerrainHeight(new Vector2Int(StartX, row)), row);
    }

    public float GetTrafficReactionTimeScaleForCurrentScore()
    {
        TrafficTier startingTier = GetTrafficTier(0);
        TrafficTier currentTier = GetTrafficTier(score);

        if (startingTier == null || currentTier == null || startingTier.TargetReactionTime <= 0f || currentTier.TargetReactionTime <= 0f)
        {
            return 1f;
        }

        return Mathf.Max(0.01f, currentTier.TargetReactionTime / startingTier.TargetReactionTime);
    }

    public void PlayCameraShake(float duration, float magnitude)
    {
        if (cameraShakeRoutine != null)
        {
            StopCoroutine(cameraShakeRoutine);
        }

        cameraShakeRoutine = StartCoroutine(ScreenShake(Mathf.Max(0f, duration), Mathf.Abs(magnitude)));
    }

    public void OnPlayerDeath(DeathData data)
    {
        Time.timeScale = 0f;

        DeathScreen screen = ResolveDeathScreen();
        if (screen == null)
        {
            Debug.LogWarning($"{nameof(GameManager)} cannot show the death screen because no {nameof(DeathScreen)} is assigned or present in the scene.", this);
            return;
        }

        screen.Show(data != null ? data : ChooseDeathData(), score, coinsCollected);
    }

    private IEnumerator ScreenShake(float duration, float magnitude)
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            cameraShakeRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            Vector3 offset = Random.insideUnitSphere * magnitude;
            cameraShakeOffset = new Vector3(offset.x, offset.y, 0f);
            cam.transform.position = cameraBasePos + cameraShakeOffset;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        cameraShakeOffset = Vector3.zero;
        cam.transform.position = cameraBasePos;
        cameraShakeRoutine = null;
    }

    private IEnumerator FlashMakDeathScreen()
    {
        Image flashImage = ResolveMakDeathFlashImage();
        if (flashImage == null)
        {
            if (MakDeathFlashDuration > 0f)
            {
                yield return new WaitForSecondsRealtime(MakDeathFlashDuration);
            }

            yield break;
        }

        flashImage.gameObject.SetActive(true);
        flashImage.transform.SetAsLastSibling();
        flashImage.color = new Color(1f, 0f, 0f, MakDeathFlashAlpha);

        if (MakDeathFlashDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(MakDeathFlashDuration);
        }

        ClearMakDeathFlash();
    }

    private Image ResolveMakDeathFlashImage()
    {
        if (makDeathFlashImage != null)
        {
            return makDeathFlashImage;
        }

        Canvas targetCanvas = ResolveDeathScreen()?.GetComponentInParent<Canvas>();
        if (targetCanvas == null)
        {
            targetCanvas = FindFirstObjectByType<Canvas>(FindObjectsInactive.Include);
        }

        if (targetCanvas == null)
        {
            GameObject canvasObject = new("MakDeathFlashCanvas", typeof(Canvas), typeof(CanvasScaler));
            makDeathFlashCanvasObject = canvasObject;
            targetCanvas = canvasObject.GetComponent<Canvas>();
            targetCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            targetCanvas.sortingOrder = short.MaxValue;
        }

        GameObject flashObject = new("MakDeathFlash", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        flashObject.transform.SetParent(targetCanvas.transform, false);
        flashObject.transform.SetAsLastSibling();

        RectTransform rectTransform = flashObject.GetComponent<RectTransform>();
        rectTransform.anchorMin = Vector2.zero;
        rectTransform.anchorMax = Vector2.one;
        rectTransform.offsetMin = Vector2.zero;
        rectTransform.offsetMax = Vector2.zero;

        makDeathFlashImage = flashObject.GetComponent<Image>();
        makDeathFlashImage.raycastTarget = false;
        makDeathFlashImage.color = new Color(1f, 0f, 0f, 0f);
        return makDeathFlashImage;
    }

    private void ClearMakDeathFlash()
    {
        if (makDeathFlashImage != null)
        {
            Destroy(makDeathFlashImage.gameObject);
            makDeathFlashImage = null;
        }

        if (makDeathFlashCanvasObject != null)
        {
            Destroy(makDeathFlashCanvasObject);
            makDeathFlashCanvasObject = null;
        }
    }

    private void SetScoreUiVisible(bool visible)
    {
        if (scoreLabel != null)
        {
            scoreLabel.gameObject.SetActive(visible);
        }

        if (scoreText != null)
        {
            scoreText.gameObject.SetActive(visible);
        }
    }

    private void ValidateTrafficTiers()
    {
        if (trafficTiers == null)
        {
            trafficTiers = new List<TrafficTier>();
        }

        foreach (TrafficTier tier in trafficTiers)
        {
            tier?.Validate();
        }

        trafficTiers.Sort((left, right) =>
        {
            if (left == null && right == null)
            {
                return 0;
            }

            if (left == null)
            {
                return 1;
            }

            if (right == null)
            {
                return -1;
            }

            return left.MinRow.CompareTo(right.MinRow);
        });
    }

    private TrafficTier GetTrafficTier(int row)
    {
        ValidateTrafficTiers();

        TrafficTier selectedTier = null;
        foreach (TrafficTier tier in trafficTiers)
        {
            if (tier == null)
            {
                continue;
            }

            if (row >= tier.MinRow)
            {
                selectedTier = tier;
                continue;
            }

            break;
        }

        return selectedTier;
    }

    private float GetRoadProbability(int row)
    {
        TrafficTier tier = GetTrafficTier(row);
        return tier != null ? tier.RoadProbability : 0f;
    }

    private Road.SpawnConfig BuildRoadConfig(TrafficTier tier)
    {
        float positiveDirectionChance = DefaultPositiveDirectionChance;
        if (characterPos.x <= leftEdgeThreshold)
        {
            positiveDirectionChance += leftEdgeDirectionBias;
        }

        return new Road.SpawnConfig
        {
            MinSpeed = tier != null ? Mathf.Max(MinTrafficSpeed, Mathf.Min(tier.MinSpeed, tier.MaxSpeed)) : 0f,
            MaxSpeed = tier != null ? Mathf.Max(tier.MinSpeed, tier.MaxSpeed) : 0f,
            MinVehicleCount = tier != null ? tier.MinVehicleCount : 0,
            MaxVehicleCount = tier != null ? tier.MaxVehicleCount : 0,
            TargetReactionTime = tier != null ? tier.TargetReactionTime : 0f,
            ReactionTimeJitter = tier != null ? tier.ReactionTimeJitter : 0f,
            MinGuaranteedSafeWindow = tier != null ? tier.GuaranteedSafeWindow : 0f,
            PositiveDirectionChance = Mathf.Clamp01(positiveDirectionChance),
            WrapX = trafficWrapX
        };
    }

    private void TryShowMilestone(int currentScore)
    {
        if (milestoneRows == null)
        {
            return;
        }

        for (int i = 0; i < milestoneRows.Length; i++)
        {
            int milestone = milestoneRows[i];
            if (milestone <= 0 || currentScore != milestone || shownMilestones.Contains(milestone))
            {
                continue;
            }

            shownMilestones.Add(milestone);
            ShowMilestone(string.Format(milestoneFormat, milestone));
            return;
        }
    }

    private void ShowMilestone(string message)
    {
        TextMeshProUGUI target = ResolveMilestoneText();
        if (target == null)
        {
            return;
        }

        if (milestoneRoutine != null)
        {
            StopCoroutine(milestoneRoutine);
        }

        milestoneRoutine = StartCoroutine(FlashMilestone(target, message));
    }

    private TextMeshProUGUI ResolveMilestoneText()
    {
        if (milestoneText != null)
        {
            return milestoneText;
        }

        if (scoreText == null || scoreText.transform.parent == null)
        {
            return null;
        }

        GameObject milestoneObject = new("MilestoneText", typeof(RectTransform));
        milestoneObject.transform.SetParent(scoreText.transform.parent, false);
        milestoneText = milestoneObject.AddComponent<TextMeshProUGUI>();
        milestoneText.alignment = TextAlignmentOptions.Center;
        milestoneText.fontSize = MilestoneFontSize;
        milestoneText.fontStyle = FontStyles.Bold;
        milestoneText.color = Color.white;
        milestoneText.raycastTarget = false;
        milestoneText.gameObject.SetActive(false);

        RectTransform rectTransform = milestoneText.rectTransform;
        rectTransform.anchorMin = new Vector2(MilestoneAnchorX, MilestoneAnchorY);
        rectTransform.anchorMax = new Vector2(MilestoneAnchorX, MilestoneAnchorY);
        rectTransform.pivot = new Vector2(MilestoneAnchorX, MilestoneAnchorX);
        rectTransform.anchoredPosition = Vector2.zero;
        rectTransform.sizeDelta = new Vector2(MilestoneWidth, MilestoneHeight);

        return milestoneText;
    }

    private IEnumerator FlashMilestone(TextMeshProUGUI target, string message)
    {
        target.text = message;
        target.gameObject.SetActive(true);

        if (milestoneFlashDuration > 0f)
        {
            yield return new WaitForSecondsRealtime(milestoneFlashDuration);
        }

        target.gameObject.SetActive(false);
        milestoneRoutine = null;
    }

    private void HideMilestoneImmediate()
    {
        if (milestoneRoutine != null)
        {
            StopCoroutine(milestoneRoutine);
            milestoneRoutine = null;
        }

        if (milestoneText != null)
        {
            milestoneText.gameObject.SetActive(false);
        }
    }

    private void HideDeathScreenImmediate()
    {
        DeathScreen screen = ResolveDeathScreen();
        if (screen != null)
        {
            screen.HideImmediate();
        }
    }

    private void ResolveMakController()
    {
        if (makController == null)
        {
            makController = GetComponent<MakController>();
        }

        if (makController == null)
        {
            makController = gameObject.AddComponent<MakController>();
        }

        makController.Init(this);
    }

    private DeathScreen ResolveDeathScreen()
    {
        if (deathScreen != null)
        {
            return deathScreen;
        }

        deathScreen = FindFirstObjectByType<DeathScreen>(FindObjectsInactive.Include);
        if (deathScreen != null)
        {
            return deathScreen;
        }

        return null;
    }

    private DeathData ChooseDeathData()
    {
        if (deathPresets == null || deathPresets.Length == 0)
        {
            deathPresets = Resources.LoadAll<DeathData>("DeathData");
        }

        if (deathPresets != null && deathPresets.Length > 0)
        {
            return deathPresets[Random.Range(0, deathPresets.Length)];
        }

        return CreateFallbackDeathData();
    }

    private static DeathData CreateMakDeathData()
    {
        return DeathData.CreateRuntime(
            "Mak",
            "Mak Dah Sampai,\nPemain Kantoi",
            "Mak catches player from behind",
            "\"Jangan lambat sangat.\"",
            "\"Don't take too long.\"",
            "Mak mengejar dari lorong belakang.\nLarian tamat di baris terakhir.");
    }

    private static DeathData CreateFallbackDeathData()
    {
        int index = Random.Range(0, FallbackDeathDataVariantCount);
        return index switch
        {
            0 => DeathData.CreateRuntime("Myvi", "Mangsa Kena Langgar Myvi\nDi Lorong Sempit", "Victim struck by Myvi in a narrow lane", "\"Semua orang tahu Myvi bawak laju. Tapi dia tetap tak elak.\"", "\"Everyone knows Myvis drive fast. He still didn't dodge.\"", "Allahyarham dikenali gemar makan nasi lemak pagi.\nSemoga rohnya tenang di jalan yang lebih selamat."),
            1 => DeathData.CreateRuntime("Longkang", "Lelaki Tersasar Masuk\nLongkang Besar", "Man falls into massive monsoon drain", "\"Mak cik jiran dah warning minggu lepas. Dia tak dengar juga.\"", "\"The neighbour auntie warned him last week. He didn't listen.\"", "Mangsa dijumpai terapung bersama kasut sebelah.\nSiasatan sedang dijalankan."),
            2 => DeathData.CreateRuntime("Durian", "Buah Durian Jatuh Mengejut,\nSeorang Terkorban", "Falling durian claims one unsuspecting victim", "\"Mati sebab durian. Sebenarnya, memang worth it.\"", "\"Death by durian. Honestly, worth it.\"", "Wangian durian masih tercium di lokasi kejadian.\nPihak berkuasa mohon orang ramai jauhi pokok berkenaan."),
            3 => DeathData.CreateRuntime("Cuaca", "Tak Tahan Panas Malaysia,\nPancit Di Tengah Jalan", "Unable to withstand Malaysian heat, collapses mid-road", "\"Dah tahu panas, kenapa tak bawa air? Soalan polis.\"", "\"You knew it was hot. Why no water? Police are asking.\"", "Ini adalah kematian ke-3 akibat panas terik minggu ini.\nJPN mohon rakyat sentiasa bawa payung."),
            _ => DeathData.CreateRuntime("Kucing", "Kucing Tidur Di Jalan,\nPemain Tak Sanggup Elak", "Cat sleeping on road, player unable to swerve", "\"Kucing tu memang bos kawasan situ. Semua orang pun tahu.\"", "\"That cat is the boss of this area. Everyone knows it.\"", "Kucing berkenaan masih tidur di lokasi kejadian.\nTidak memberikan sebarang kenyataan kepada media.")
        };
    }

    private void UpdateScoreText()
    {
        if (scoreText != null)
        {
            scoreText.text = score.ToString();
        }
    }

    private void ConfigureCharacterBody()
    {
        if (characterBody == null)
        {
            return;
        }

        // The grid controller owns position while the non-kinematic body still receives
        // collision callbacks from kinematic vehicle prefabs.
        characterBody.useGravity = false;
        characterBody.isKinematic = false;
        characterBody.constraints = RigidbodyConstraints.FreezeAll;
    }

    private void SetCharacterPosition(Vector3 position)
    {
        if (characterBody != null)
        {
            characterBody.position = position;
            character.position = characterBody.position;
            Physics.SyncTransforms();
        }
    }

    private void SyncCharacterTransformToBody()
    {
        if (characterBody == null)
        {
            return;
        }

        character.position = characterBody.position;
        Physics.SyncTransforms();
    }

    private void CacheHopAnimationDuration()
    {
        if (characterAnimator?.runtimeAnimatorController == null)
        {
            return;
        }

        foreach (AnimationClip clip in characterAnimator.runtimeAnimatorController.animationClips)
        {
            if (clip != null && clip.name == "Hop")
            {
                hopAnimationDuration = clip.length;
                return;
            }
        }
    }

    private void PlayHopAnimation()
    {
        if (characterAnimator == null)
        {
            return;
        }

        characterAnimator.speed = Mathf.Max(1f, hopAnimationDuration / moveDuration);
        characterAnimator.SetTrigger("Hop");
    }

    private void ResetHopAnimationSpeed()
    {
        if (characterAnimator != null)
        {
            characterAnimator.speed = 1f;
        }
    }
}
