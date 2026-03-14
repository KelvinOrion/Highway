using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    public struct SpawnConfig
    {
        public float minSpeed;
        public float maxSpeed;
        public int minVehicleCount;
        public int maxVehicleCount;
        public float targetReactionTime;
        public float reactionTimeJitter;
        public float minGuaranteedSafeWindow;
        public float positiveDirectionChance;
        public float wrapX;
    }

    [SerializeField] private List<Rigidbody> vehicles;
    
    // Per-vehicle height offsets (default 0.1f for road surface, adjust if asset has different pivot)
    [SerializeField] private List<float> vehicleHeightOffsets = new();
    
    // Per-vehicle rotation offset (in case you have mixed assets with different orientations)
    [SerializeField] private List<float> vehicleRotationOffsets = new();

    private int direction = 1;
    private float speed = 1f;
    private List<Rigidbody> spawnedVehicles = new();
    private float wrapX = 15f;

    public HashSet<int> Init(float z, SpawnConfig config)
    {
        //Place obstacles at the location provided
        transform.position = new Vector3(0, 0, z);
        spawnedVehicles.Clear();

        if ((vehicles == null) || (vehicles.Count == 0))
        {
            return new() { -10, 10 };
        }

        int minCount = Mathf.Max(0, config.minVehicleCount);
        int maxCount = Mathf.Max(minCount, config.maxVehicleCount);
        float minSpeed = Mathf.Max(0.5f, Mathf.Min(config.minSpeed, config.maxSpeed));
        float maxSpeed = Mathf.Max(minSpeed, Mathf.Max(config.minSpeed, config.maxSpeed));

        wrapX = Mathf.Max(6f, config.wrapX);

        //Choose direction with optional weighted edge pressure.
        direction = Random.value < config.positiveDirectionChance ? 1 : -1;

        speed = Random.Range(minSpeed, maxSpeed);

        //Choose which vehicles and how many to use in this loop.
        int idx = Random.Range(0, vehicles.Count);
        int vehicleCount = Random.Range(minCount, maxCount + 1);

        //Convert target reaction-time into distance gap so speed and gap stay coupled.
        float reactionTime = Mathf.Max(
            0.3f,
            config.targetReactionTime + Random.Range(-config.reactionTimeJitter, config.reactionTimeJitter)
        );
        float laneLength = wrapX * 2f;

        //Keep at least one guaranteed winnable window in every loop.
        float maxBlockedDistance = laneLength - (speed * config.minGuaranteedSafeWindow);
        float maxByCount = vehicleCount > 1 ? maxBlockedDistance / (vehicleCount - 1) : laneLength;
        float baseGap = speed * reactionTime;
        float gap = Mathf.Clamp(baseGap, 1.5f, Mathf.Max(1.5f, maxByCount));

        //Instantiate the vehicles with adjusted rotation and height per asset type
        for (int i = 0; i < vehicleCount; i++)
        {
            // Get height offset for this vehicle type (default to 0.1f if not set)
            float heightOffset = (idx < vehicleHeightOffsets.Count) ? vehicleHeightOffsets[idx] : 0.1f;
            
            // Get rotation offset for this vehicle type (default to 0 if not set)
            float rotationOffset = (idx < vehicleRotationOffsets.Count) ? vehicleRotationOffsets[idx] : 0f;
            
            // Apply rotation: base direction + per-vehicle offset
            Quaternion rotation = Quaternion.Euler(0f, (90f * direction) + rotationOffset, 0f);
            
            Rigidbody vehicle = Instantiate(
                vehicles[idx],
                new Vector3((i * gap) * -direction, heightOffset, z),
                rotation,
                transform
                );
            spawnedVehicles.Add(vehicle);
        }

        //The only obstacles are outside the game area
        return new() { -10, 10 };
    }

    private void FixedUpdate()
    {
        //Move vehicles
        foreach (Rigidbody vehicle in spawnedVehicles)
        {
            ///Move along road, use the RB movement so collision are handled correctly
            Vector3 moveAmount = new(speed * direction * Time.fixedDeltaTime, 0f, 0f);
            vehicle.MovePosition(vehicle.position + moveAmount);

            //Wrap around when they are off camera
            Vector3 pos = vehicle.position;
            if ((direction > 0) && (pos.x > wrapX))
            {
                pos.x = -wrapX;
                vehicle.position = pos;
            }
            else if ((direction < 0) && (pos.x < -wrapX))
            {
                pos.x = wrapX;
                vehicle.position = pos;
            }
        }
    }
}
