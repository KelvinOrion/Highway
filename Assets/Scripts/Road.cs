using System.Collections.Generic;
using UnityEngine;

public class Road : MonoBehaviour
{
    [SerializeField] private List<Rigidbody> vehicles;
    
    // Per-vehicle height offsets (default 0.1f for road surface, adjust if asset has different pivot)
    [SerializeField] private List<float> vehicleHeightOffsets = new();
    
    // Per-vehicle rotation offset (in case you have mixed assets with different orientations)
    [SerializeField] private List<float> vehicleRotationOffsets = new();

    private int direction = 1;
    private float speed = 1f;
    private List<Rigidbody> spawnedVehicles = new();

    public HashSet<int> Init(float z)
    {
        //Place obstacles at the location provided
        transform.position = new Vector3(0, 0, z);

        //Choose which direction the vehicles go, -1 or +1.
        direction = 2 * Random.Range(0, 2) - 1;

        //Choose the speed, we make them faster as we progress
        float minSpeed = Mathf.Lerp(1f, 3f, z / 500f);
        float maxSpeed = Mathf.Lerp(3f, 8f, z / 500f);
        speed = Random.Range(minSpeed, maxSpeed);

        //choose which vehicles, how many, how far apart they are.
        int idx = Random.Range(0, vehicles.Count);
        int vehicleCount = Random.Range(0, 4);
        float gap = Random.Range(2f, 8f);

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
            if ((direction > 0) && (pos.x > 15))
            {
                pos.x = -15;
                vehicle.position = pos;
            }
            else if ((direction < 0) && (pos.x < -15))
            {
                pos.x = 15;
                vehicle.position = pos;
            }
        }
    }
}
