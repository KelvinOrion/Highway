using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    public struct SpawnConfig
    {
        public float MinSpeed;
        public float MaxSpeed;
        public int MinVehicleCount;
        public int MaxVehicleCount;
        public float TargetReactionTime;
        public float ReactionTimeJitter;
        public float MinGuaranteedSafeWindow;
        public float PositiveDirectionChance;
        public float WrapX;
    }

    private const int PlayableMinX = -10;
    private const int PlayableMaxX = 10;
    private const float DefaultVehicleHeight = 0.1f;
    private const float WrapMinX = -15f;
    private const float WrapMaxX = 15f;
    private const float MinSpeedStart = 1f;
    private const float MinSpeedEnd = 3f;
    private const float MaxSpeedStart = 3f;
    private const float MaxSpeedEnd = 8f;
    private const float SpeedMaxDistance = 500f;
    private const int MinVehicleCountInclusive = 0;
    private const int MaxVehicleCountExclusive = 4;
    private const float MinVehicleGap = 2f;
    private const float MaxVehicleGap = 8f;
    private const float VehicleYawBase = 90f;
    private const float MinReactionTime = 0.3f;
    private const float MinSafeGap = 1.5f;
    private const float MinConfigWrapX = 6f;
    private const float DefaultPositiveDirectionChance = 0.5f;
    private const float DefaultTargetReactionTime = 1f;
    private const float MinCenterLineDashLength = 0.01f;
    private const float MinCenterLineDashGap = 0.01f;
    private const float MinCenterLineWidth = 0.01f;
    private const float MinCenterLineThickness = 0.001f;

    [SerializeField] private List<Rigidbody> vehicles;
    [SerializeField] private RoadPotholeSpawner potholeSpawner;
    [SerializeField] private Renderer[] roadSurfaceRenderers;

    [Header("Center Line")]
    [SerializeField] private float centerLineDashLength = 1.2f;
    [SerializeField] private float centerLineDashGap = 0.9f;
    [SerializeField] private float centerLineWidth = 0.08f;
    [SerializeField] private float centerLineThickness = 0.02f;
    [SerializeField] private float centerLineSurfaceOffset = 0.012f;

    // Per-vehicle adjustments let mixed model assets share the same road logic.
    [SerializeField] private List<float> vehicleHeightOffsets = new();
    [SerializeField] private List<float> vehicleRotationOffsets = new();

    private int direction = 1;
    private float speed = 1f;
    private float wrapMinX = WrapMinX;
    private float wrapMaxX = WrapMaxX;
    private readonly List<Rigidbody> spawnedVehicles = new();
    private readonly List<GameObject> spawnedCenterLineDashes = new();

    private static Material centerLineMaterial;
    private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorProperty = Shader.PropertyToID("_Color");

    public HashSet<int> Init(float z)
    {
        return Init(z, ChooseDefaultSpawnConfig(z));
    }

    public HashSet<int> Init(float z, SpawnConfig config)
    {
        transform.position = new Vector3(0, 0, z);

        potholeSpawner ??= GetComponent<RoadPotholeSpawner>();
        potholeSpawner?.RefreshPotholes();
        RefreshCenterLineDashes();

        float positiveDirectionChance = Mathf.Clamp01(config.PositiveDirectionChance);
        if (Mathf.Approximately(positiveDirectionChance, 0f))
        {
            positiveDirectionChance = DefaultPositiveDirectionChance;
        }

        direction = Random.value < positiveDirectionChance ? 1 : -1;
        ConfigureWrapBounds(config.WrapX);

        float minSpeed = Mathf.Max(MinSpeedStart, Mathf.Min(config.MinSpeed, config.MaxSpeed));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(config.MinSpeed, config.MaxSpeed));
        speed = Random.Range(minSpeed, maxSpeed);

        if (vehicles == null || vehicles.Count == 0)
        {
            Debug.LogWarning($"{nameof(Road)} has no vehicle prefabs assigned.", this);
            return RoadEdgeObstacles();
        }

        int vehicleIndex = Random.Range(0, vehicles.Count);
        int minVehicleCount = Mathf.Max(MinVehicleCountInclusive, config.MinVehicleCount);
        int maxVehicleCount = Mathf.Max(minVehicleCount, config.MaxVehicleCount);
        int vehicleCount = Random.Range(minVehicleCount, maxVehicleCount + 1);
        float gap = CalculateSafeGap(vehicleCount, config);

        for (int i = 0; i < vehicleCount; i++)
        {
            Rigidbody prefab = vehicles[vehicleIndex];
            if (prefab == null)
            {
                Debug.LogWarning($"{nameof(Road)} has an empty vehicle prefab slot at index {vehicleIndex}.", this);
                continue;
            }

            float heightOffset = GetListValue(vehicleHeightOffsets, vehicleIndex, DefaultVehicleHeight);
            float rotationOffset = GetListValue(vehicleRotationOffsets, vehicleIndex, 0f);
            Quaternion rotation = Quaternion.Euler(0f, (VehicleYawBase * direction) + rotationOffset, 0f);

            Rigidbody vehicle = Instantiate(
                prefab,
                new Vector3((i * gap) * -direction, heightOffset, z),
                rotation,
                transform);

            if (HandPowerup.IsActive)
            {
                HandPowerup.FreezeVehicle(vehicle);
            }

            spawnedVehicles.Add(vehicle);
        }

        return RoadEdgeObstacles();
    }

    private void OnValidate()
    {
        centerLineDashLength = Mathf.Max(MinCenterLineDashLength, centerLineDashLength);
        centerLineDashGap = Mathf.Max(MinCenterLineDashGap, centerLineDashGap);
        centerLineWidth = Mathf.Max(MinCenterLineWidth, centerLineWidth);
        centerLineThickness = Mathf.Max(MinCenterLineThickness, centerLineThickness);
        centerLineSurfaceOffset = Mathf.Max(0f, centerLineSurfaceOffset);
    }

    private void FixedUpdate()
    {
        if (HandPowerup.IsActive)
        {
            return;
        }

        foreach (Rigidbody vehicle in spawnedVehicles)
        {
            if (vehicle == null)
            {
                continue;
            }

            Vector3 moveAmount = new(speed * direction * Time.fixedDeltaTime, 0f, 0f);
            vehicle.MovePosition(vehicle.position + moveAmount);

            Vector3 pos = vehicle.position;
            if (direction > 0 && pos.x > wrapMaxX)
            {
                pos.x = wrapMinX;
                vehicle.position = pos;
            }
            else if (direction < 0 && pos.x < wrapMinX)
            {
                pos.x = wrapMaxX;
                vehicle.position = pos;
            }
        }
    }

    private void ConfigureWrapBounds(float configuredWrapX)
    {
        float wrapX = configuredWrapX > 0f
            ? Mathf.Max(MinConfigWrapX, configuredWrapX)
            : WrapMaxX;

        wrapMinX = -wrapX;
        wrapMaxX = wrapX;
    }

    private void RefreshCenterLineDashes()
    {
        ClearCenterLineDashes();

        if (!TryGetRoadBounds(out Bounds bounds))
        {
            Debug.LogWarning($"{nameof(Road)} could not find road surface bounds for center line dashes.", this);
            return;
        }

        float dashStep = centerLineDashLength + centerLineDashGap;
        float y = bounds.max.y + centerLineSurfaceOffset + centerLineThickness * 0.5f;
        float z = bounds.center.z;

        for (float startX = bounds.min.x; startX < bounds.max.x; startX += dashStep)
        {
            float dashLength = Mathf.Min(centerLineDashLength, bounds.max.x - startX);
            if (dashLength <= 0f)
            {
                continue;
            }

            SpawnCenterLineDash(new Vector3(startX + dashLength * 0.5f, y, z), dashLength);
        }
    }

    private void ClearCenterLineDashes()
    {
        for (int i = spawnedCenterLineDashes.Count - 1; i >= 0; i--)
        {
            GameObject dash = spawnedCenterLineDashes[i];
            if (dash == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(dash);
            }
            else
            {
                DestroyImmediate(dash);
            }
        }

        spawnedCenterLineDashes.Clear();
    }

    private bool TryGetRoadBounds(out Bounds bounds)
    {
        Renderer[] renderers = ResolveRoadSurfaceRenderers();
        bounds = default;
        bool hasBounds = false;

        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer roadRenderer = renderers[i];
            if (roadRenderer == null)
            {
                continue;
            }

            if (!hasBounds)
            {
                bounds = roadRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                bounds.Encapsulate(roadRenderer.bounds);
            }
        }

        return hasBounds;
    }

    private Renderer[] ResolveRoadSurfaceRenderers()
    {
        if (roadSurfaceRenderers != null && roadSurfaceRenderers.Length > 0)
        {
            return roadSurfaceRenderers;
        }

        roadSurfaceRenderers = GetComponentsInChildren<MeshRenderer>();
        return roadSurfaceRenderers;
    }

    private void SpawnCenterLineDash(Vector3 worldPosition, float dashLength)
    {
        GameObject dash = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dash.name = "Center Line Dash";
        dash.transform.position = worldPosition;
        dash.transform.SetParent(transform, true);
        dash.transform.localScale = new Vector3(dashLength, centerLineThickness, centerLineWidth);

        Collider dashCollider = dash.GetComponent<Collider>();
        if (dashCollider != null)
        {
            Destroy(dashCollider);
        }

        Renderer dashRenderer = dash.GetComponent<Renderer>();
        Material material = GetCenterLineMaterial();
        if (dashRenderer != null && material != null)
        {
            dashRenderer.sharedMaterial = material;
            dashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            dashRenderer.receiveShadows = false;
        }

        spawnedCenterLineDashes.Add(dash);
    }

    private static Material GetCenterLineMaterial()
    {
        if (centerLineMaterial != null)
        {
            return centerLineMaterial;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Unlit/Color") ??
                        Shader.Find("Sprites/Default");
        if (shader == null)
        {
            return null;
        }

        centerLineMaterial = new Material(shader)
        {
            name = "Procedural Center Line",
            hideFlags = HideFlags.DontSave
        };
        SetMaterialColor(centerLineMaterial, Color.white);
        return centerLineMaterial;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material.HasProperty(BaseColor))
        {
            material.SetColor(BaseColor, color);
        }

        if (material.HasProperty(ColorProperty))
        {
            material.SetColor(ColorProperty, color);
        }
    }

    private float CalculateSafeGap(int vehicleCount, SpawnConfig config)
    {
        float fallbackGap = Random.Range(MinVehicleGap, MaxVehicleGap);
        if (vehicleCount <= 0)
        {
            return fallbackGap;
        }

        float reactionTime = Mathf.Max(
            MinReactionTime,
            config.TargetReactionTime + Random.Range(-config.ReactionTimeJitter, config.ReactionTimeJitter));
        float laneLength = Mathf.Abs(wrapMaxX - wrapMinX);
        float maxBlockedDistance = laneLength - speed * Mathf.Max(0f, config.MinGuaranteedSafeWindow);
        float maxGapByCount = vehicleCount > 1
            ? maxBlockedDistance / (vehicleCount - 1)
            : laneLength;
        float reactionGap = speed * reactionTime;

        return Mathf.Clamp(reactionGap, MinSafeGap, Mathf.Max(MinSafeGap, maxGapByCount));
    }

    private static float GetListValue(IReadOnlyList<float> values, int index, float fallback)
    {
        return values != null && index >= 0 && index < values.Count ? values[index] : fallback;
    }

    private SpawnConfig ChooseDefaultSpawnConfig(float z)
    {
        float progress = z / SpeedMaxDistance;
        return new SpawnConfig
        {
            MinSpeed = Mathf.Lerp(MinSpeedStart, MinSpeedEnd, progress),
            MaxSpeed = Mathf.Lerp(MaxSpeedStart, MaxSpeedEnd, progress),
            MinVehicleCount = MinVehicleCountInclusive,
            MaxVehicleCount = MaxVehicleCountExclusive - 1,
            TargetReactionTime = DefaultTargetReactionTime,
            ReactionTimeJitter = 0f,
            MinGuaranteedSafeWindow = 0f,
            PositiveDirectionChance = DefaultPositiveDirectionChance,
            WrapX = WrapMaxX
        };
    }

    private static HashSet<int> RoadEdgeObstacles()
    {
        return new HashSet<int> { PlayableMinX, PlayableMaxX };
    }
}
