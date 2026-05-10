using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    private const int PlayableMinX = -10;
    private const int PlayableMaxX = 10;
    private const float DefaultVehicleHeight = 0.1f;
    private const float WrapMinX = -15f;
    private const float WrapMaxX = 15f;
    private const int DirectionMultiplier = 2;
    private const int DirectionOptionCount = 2;
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
    private readonly List<Rigidbody> spawnedVehicles = new();
    private MalaysianRoadTextureController.RoadType currentRoadType;

    public MalaysianRoadTextureController.RoadType CurrentRoadType => currentRoadType;

    public HashSet<int> Init(float z)
    {
        return Init(z, ChooseRoadType());
    }

    public HashSet<int> Init(float z, MalaysianRoadTextureController.RoadType roadType)
    {
        transform.position = new Vector3(0, 0, z);
        currentRoadType = roadType;

        roadTextureController ??= GetComponent<MalaysianRoadTextureController>();
        roadTextureController?.SetRoadType(currentRoadType);

        potholeSpawner ??= GetComponent<RoadPotholeSpawner>();
        potholeSpawner?.RefreshPotholes();

        direction = DirectionMultiplier * Random.Range(0, DirectionOptionCount) - 1;

        float minSpeed = Mathf.Lerp(MinSpeedStart, MinSpeedEnd, z / SpeedMaxDistance);
        float maxSpeed = Mathf.Lerp(MaxSpeedStart, MaxSpeedEnd, z / SpeedMaxDistance);
        speed = Random.Range(minSpeed, maxSpeed);

        if (vehicles == null || vehicles.Count == 0)
        {
            Debug.LogWarning($"{nameof(Road)} has no vehicle prefabs assigned.", this);
            return RoadEdgeObstacles();
        }

        int vehicleIndex = Random.Range(0, vehicles.Count);
        int vehicleCount = Random.Range(MinVehicleCountInclusive, MaxVehicleCountExclusive);
        float gap = Random.Range(MinVehicleGap, MaxVehicleGap);

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
            if (direction > 0 && pos.x > WrapMaxX)
            {
                pos.x = WrapMinX;
                vehicle.position = pos;
            }
            else if (direction < 0 && pos.x < WrapMinX)
            {
                pos.x = WrapMaxX;
                vehicle.position = pos;
            }
        }
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

    private static HashSet<int> RoadEdgeObstacles()
    {
        return new HashSet<int> { PlayableMinX, PlayableMaxX };
    }
}
