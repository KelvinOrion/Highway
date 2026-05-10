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
        public MalaysianRoadTextureController.RoadType? RoadType;
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

    [SerializeField] private List<Rigidbody> vehicles;
    [SerializeField] private RoadPotholeSpawner potholeSpawner;
    [SerializeField] private MalaysianRoadTextureController roadTextureController;

    [Header("Road Surface")]
    [SerializeField] private bool randomizeRoadType = true;
    [SerializeField] private MalaysianRoadTextureController.RoadType defaultRoadType = MalaysianRoadTextureController.RoadType.MultiLaneWithEmergencyShoulder;
    [SerializeField] private List<MalaysianRoadTextureController.RoadType> roadTypePool = new()
    {
        MalaysianRoadTextureController.RoadType.SingleNoLine,
        MalaysianRoadTextureController.RoadType.SingleDoubleLine,
        MalaysianRoadTextureController.RoadType.MultiLane,
        MalaysianRoadTextureController.RoadType.MultiLaneWithEmergencyShoulder
    };

    // Per-vehicle adjustments let mixed model assets share the same road logic.
    [SerializeField] private List<float> vehicleHeightOffsets = new();
    [SerializeField] private List<float> vehicleRotationOffsets = new();

    private int direction = 1;
    private float speed = 1f;
    private float wrapMinX = WrapMinX;
    private float wrapMaxX = WrapMaxX;
    private readonly List<Rigidbody> spawnedVehicles = new();
    private MalaysianRoadTextureController.RoadType currentRoadType;

    public MalaysianRoadTextureController.RoadType CurrentRoadType => currentRoadType;

    public HashSet<int> Init(float z)
    {
        return Init(z, ChooseDefaultSpawnConfig(z));
    }

    public HashSet<int> Init(float z, MalaysianRoadTextureController.RoadType roadType)
    {
        SpawnConfig config = ChooseDefaultSpawnConfig(z);
        config.RoadType = roadType;
        return Init(z, config);
    }

    public HashSet<int> Init(float z, SpawnConfig config)
    {
        transform.position = new Vector3(0, 0, z);
        currentRoadType = config.RoadType.HasValue ? config.RoadType.Value : ChooseRoadType();

        roadTextureController ??= GetComponent<MalaysianRoadTextureController>();
        roadTextureController?.SetRoadType(currentRoadType);

        potholeSpawner ??= GetComponent<RoadPotholeSpawner>();
        potholeSpawner?.RefreshPotholes();

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

            spawnedVehicles.Add(vehicle);
        }

        return RoadEdgeObstacles();
    }

    private void OnValidate()
    {
        if (roadTypePool == null)
        {
            roadTypePool = new List<MalaysianRoadTextureController.RoadType>();
        }
    }

    private void FixedUpdate()
    {
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

    private MalaysianRoadTextureController.RoadType ChooseRoadType()
    {
        if (!randomizeRoadType || roadTypePool == null || roadTypePool.Count == 0)
        {
            return defaultRoadType;
        }

        return roadTypePool[Random.Range(0, roadTypePool.Count)];
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
            WrapX = WrapMaxX,
            RoadType = ChooseRoadType()
        };
    }

    private static HashSet<int> RoadEdgeObstacles()
    {
        return new HashSet<int> { PlayableMinX, PlayableMaxX };
    }
}
