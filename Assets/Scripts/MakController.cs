using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class MakController : MonoBehaviour
{
    private const float MinTimerInterval = 0.01f;
    private const float SpawnHopStartRowsBehind = 1.25f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");
    private static readonly HashSet<Transform> MakRoots = new();

    [Header("Spawn")]
    [SerializeField] private int spawnTriggerRow = 5;

    [Header("Leash")]
    [SerializeField] private int startingLeashDistance = 8;
    [SerializeField] private int minimumLeashDistance = 1;
    [SerializeField] private float leashShrinkInterval = 4f;
    [SerializeField] private float makHopInterval = 0.8f;

    [Header("Placeholder Visual")]
    [SerializeField] private float hopAnimationDuration = 0.25f;
    [SerializeField] private float hopArcHeight = 0.55f;
    [SerializeField] private Vector2 placeholderSize = new(1.1f, 1.1f);
    [SerializeField] private float visualHeight = 0.85f;
    [SerializeField] private int visibleRowsBehindPlayer = 4;
    [SerializeField] private Color placeholderColor = Color.white;
    [SerializeField] private Color labelColor = Color.black;

    private GameManager gameManager;
    private GameObject makRoot;
    private Transform billboardRoot;
    private Material placeholderMaterial;
    private Coroutine hopRoutine;
    private int makRow;
    private int currentLeashDistance;
    private float currentLeashShrinkInterval;
    private float shrinkTimer;
    private float hopTimer;
    private bool hasSpawnedThisGame;
    private bool isHopping;
    private bool catchTriggered;

    public bool IsSpawned => makRoot != null;
    public int Row => makRow;

    public static bool IsMakObject(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        MakRoots.RemoveWhere(root => root == null);

        Transform current = candidate.transform;
        while (current != null)
        {
            if (MakRoots.Contains(current))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    public void Init(GameManager manager)
    {
        gameManager = manager;
        ResetLeashRuntime();
    }

    public void ResetForNewGame()
    {
        if (hopRoutine != null)
        {
            StopCoroutine(hopRoutine);
            hopRoutine = null;
        }

        DestroyMakRoot();
        hasSpawnedThisGame = false;
        isHopping = false;
        catchTriggered = false;
        makRow = 0;
        ResetLeashRuntime();
    }

    public bool CheckPlayerMoveForCatch(int playerCurrentRow)
    {
        if (!hasSpawnedThisGame && playerCurrentRow >= spawnTriggerRow)
        {
            Spawn(playerCurrentRow);
        }

        return CheckRowCatch(playerCurrentRow);
    }

    private void Update()
    {
        if (!IsSpawned || catchTriggered || gameManager == null || gameManager.IsPlayerDead)
        {
            return;
        }

        float deltaTime = Time.deltaTime;
        TickLeashShrink(deltaTime);
        TickHop(deltaTime);
    }

    private void LateUpdate()
    {
        if (billboardRoot == null)
        {
            return;
        }

        UpdateVisualOffset();

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Vector3 toCamera = cam.transform.position - billboardRoot.position;
        if (toCamera.sqrMagnitude > 0.0001f)
        {
            billboardRoot.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
        }
    }

    private void OnValidate()
    {
        spawnTriggerRow = Mathf.Max(0, spawnTriggerRow);
        minimumLeashDistance = Mathf.Max(1, minimumLeashDistance);
        startingLeashDistance = Mathf.Max(minimumLeashDistance, startingLeashDistance);
        leashShrinkInterval = Mathf.Max(MinTimerInterval, leashShrinkInterval);
        makHopInterval = Mathf.Max(MinTimerInterval, makHopInterval);
        hopAnimationDuration = Mathf.Max(0f, hopAnimationDuration);
        hopArcHeight = Mathf.Max(0f, hopArcHeight);
        placeholderSize.x = Mathf.Max(0.1f, placeholderSize.x);
        placeholderSize.y = Mathf.Max(0.1f, placeholderSize.y);
        visualHeight = Mathf.Max(0f, visualHeight);
        visibleRowsBehindPlayer = Mathf.Max(1, visibleRowsBehindPlayer);
    }

    private void OnDestroy()
    {
        if (placeholderMaterial != null)
        {
            Destroy(placeholderMaterial);
            placeholderMaterial = null;
        }

        if (makRoot != null)
        {
            MakRoots.Remove(makRoot.transform);
        }
    }

    private void Spawn(int playerRow)
    {
        hasSpawnedThisGame = true;
        catchTriggered = false;
        ResetLeashRuntime();

        makRow = playerRow - currentLeashDistance;
        CreatePlaceholder();

        Vector3 endPosition = GetWorldPosition(makRow);
        Vector3 startPosition = GetWorldPosition(makRow - Mathf.CeilToInt(SpawnHopStartRowsBehind));
        makRoot.transform.position = startPosition;
        StartHopAnimation(startPosition, endPosition);
    }

    private void ResetLeashRuntime()
    {
        currentLeashDistance = Mathf.Max(minimumLeashDistance, startingLeashDistance);
        currentLeashShrinkInterval = Mathf.Max(MinTimerInterval, leashShrinkInterval);
        shrinkTimer = 0f;
        hopTimer = 0f;
    }

    private void TickLeashShrink(float deltaTime)
    {
        currentLeashShrinkInterval = GetScaledLeashShrinkInterval();

        if (currentLeashDistance <= minimumLeashDistance)
        {
            shrinkTimer = 0f;
            return;
        }

        shrinkTimer += deltaTime;
        if (shrinkTimer < currentLeashShrinkInterval)
        {
            return;
        }

        shrinkTimer -= currentLeashShrinkInterval;
        currentLeashDistance = Mathf.Max(minimumLeashDistance, currentLeashDistance - 1);
    }

    private float GetScaledLeashShrinkInterval()
    {
        float progressionScale = gameManager != null
            ? gameManager.GetTrafficReactionTimeScaleForCurrentScore()
            : 1f;

        return Mathf.Max(MinTimerInterval, leashShrinkInterval * progressionScale);
    }

    private void TickHop(float deltaTime)
    {
        if (isHopping)
        {
            return;
        }

        hopTimer += deltaTime;
        if (hopTimer < makHopInterval)
        {
            return;
        }

        hopTimer -= makHopInterval;
        HopTowardTarget();
    }

    private void HopTowardTarget()
    {
        if (gameManager == null || makRoot == null)
        {
            return;
        }

        int targetRow = gameManager.PlayerRow - currentLeashDistance;
        if (makRow >= targetRow)
        {
            return;
        }

        int nextRow = makRow + 1;
        Vector3 startPosition = makRoot.transform.position;
        makRow = nextRow;
        StartHopAnimation(startPosition, GetWorldPosition(makRow));
    }

    private bool CheckRowCatch(int playerCurrentRow)
    {
        int makCurrentRow = makRow;
        if (!IsSpawned || catchTriggered || gameManager == null || makCurrentRow < playerCurrentRow)
        {
            return false;
        }

        catchTriggered = true;
        gameManager.PlayerCaughtByMak();
        return true;
    }

    private void CreatePlaceholder()
    {
        DestroyMakRoot();

        makRoot = new GameObject("Mak");
        makRoot.transform.SetParent(transform, false);
        MakRoots.Add(makRoot.transform);

        billboardRoot = new GameObject("Mak_Billboard").transform;
        billboardRoot.SetParent(makRoot.transform, false);
        UpdateVisualOffset();

        GameObject square = GameObject.CreatePrimitive(PrimitiveType.Quad);
        square.name = "Mak_WhiteSquare";
        square.transform.SetParent(billboardRoot, false);
        square.transform.localScale = new Vector3(placeholderSize.x, placeholderSize.y, 1f);

        Collider squareCollider = square.GetComponent<Collider>();
        if (squareCollider != null)
        {
            Destroy(squareCollider);
        }

        Renderer squareRenderer = square.GetComponent<Renderer>();
        if (squareRenderer != null)
        {
            squareRenderer.sharedMaterial = GetPlaceholderMaterial();
        }

        GameObject labelObject = new("Mak_Label");
        labelObject.transform.SetParent(billboardRoot, false);
        labelObject.transform.localPosition = new Vector3(0f, 0f, 0.01f);
        labelObject.transform.localScale = Vector3.one * 0.18f;

        TextMeshPro label = labelObject.AddComponent<TextMeshPro>();
        label.text = "MAK";
        label.alignment = TextAlignmentOptions.Center;
        label.color = labelColor;
        label.fontStyle = FontStyles.Bold;
        label.enableAutoSizing = true;
        label.fontSizeMin = 1f;
        label.fontSizeMax = 5f;
        label.rectTransform.sizeDelta = new Vector2(5f, 2f);
    }

    private void DestroyMakRoot()
    {
        if (makRoot == null)
        {
            billboardRoot = null;
            return;
        }

        MakRoots.Remove(makRoot.transform);
        Destroy(makRoot);
        makRoot = null;
        billboardRoot = null;
    }

    private void UpdateVisualOffset()
    {
        if (billboardRoot == null)
        {
            return;
        }

        float visualRowOffset = 0f;
        if (gameManager != null)
        {
            // The logical row remains leash-based; this keeps the placeholder visible with the current camera framing.
            int closestVisibleRow = gameManager.PlayerRow - visibleRowsBehindPlayer;
            visualRowOffset = Mathf.Max(0f, closestVisibleRow - makRow);
        }

        billboardRoot.localPosition = new Vector3(0f, visualHeight, visualRowOffset);
    }

    private Material GetPlaceholderMaterial()
    {
        if (placeholderMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                            Shader.Find("Unlit/Color") ??
                            Shader.Find("Sprites/Default");

            if (shader == null)
            {
                return null;
            }

            placeholderMaterial = new Material(shader)
            {
                name = "Mak Placeholder",
                hideFlags = HideFlags.DontSave
            };
        }

        SetMaterialColor(placeholderMaterial, placeholderColor);
        return placeholderMaterial;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
        {
            return;
        }

        if (material.HasProperty(BaseColorId))
        {
            material.SetColor(BaseColorId, color);
        }

        if (material.HasProperty(ColorId))
        {
            material.SetColor(ColorId, color);
        }
    }

    private Vector3 GetWorldPosition(int row)
    {
        return gameManager != null
            ? gameManager.GetCenterColumnWorldPosition(row)
            : new Vector3(0f, 0.2f, row);
    }

    private void StartHopAnimation(Vector3 startPosition, Vector3 endPosition)
    {
        if (hopRoutine != null)
        {
            StopCoroutine(hopRoutine);
        }

        hopRoutine = StartCoroutine(AnimateHop(startPosition, endPosition));
    }

    private IEnumerator AnimateHop(Vector3 startPosition, Vector3 endPosition)
    {
        if (makRoot == null)
        {
            yield break;
        }

        isHopping = true;

        if (hopAnimationDuration <= 0f)
        {
            makRoot.transform.position = endPosition;
            isHopping = false;
            hopRoutine = null;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < hopAnimationDuration && makRoot != null)
        {
            float t = Mathf.Clamp01(elapsed / hopAnimationDuration);
            float arc = Mathf.Sin(t * Mathf.PI) * hopArcHeight;
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            position.y += arc;
            makRoot.transform.position = position;

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (makRoot != null)
        {
            makRoot.transform.position = endPosition;
        }

        isHopping = false;
        hopRoutine = null;
    }
}
