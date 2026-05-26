using System.Collections.Generic;
using UnityEngine;

public class Grass : MonoBehaviour
{
    private const int MinTreeCount = 1;
    private const int MaxTreeCountExclusive = 5;
    private const float TreeHeight = 0.2f;
    private const float DefaultPowerupChance = 0.08f;
    private const float PowerupHeight = 0.4f;
    private const float PowerupSpawnYOffset = 0.5f;
    private const int PowerupLaneX = 0;

    [SerializeField] private List<Transform> treePrefabs;

    [Header("Powerups")]
    [Range(0f, 1f)]
    [SerializeField] private float spawnPowerupChance = DefaultPowerupChance;
    [SerializeField] private PowerupBase[] powerupPrefabs;

    // These bounds must stay aligned with GameManager's playable x range.
    [SerializeField] private int playableAreaMinX = -10;
    [SerializeField] private int playableAreaMaxX = 10;

    private static readonly int[] YawAngles = { 0, 90, 180, 270 };

    public HashSet<int> Init(float z)
    {
        transform.position = new Vector3(0, 0, z);

        HashSet<int> locations = new() { playableAreaMinX, playableAreaMaxX };

        if (TrySpawnPowerup())
        {
            return locations;
        }

        if (treePrefabs == null || treePrefabs.Count == 0)
        {
            Debug.LogWarning($"{nameof(Grass)} has no tree prefabs assigned.", this);
            return locations;
        }

        List<int> availableColumns = BuildAvailableColumns();
        int treeCount = Mathf.Min(Random.Range(MinTreeCount, MaxTreeCountExclusive), availableColumns.Count);

        for (int i = 0; i < treeCount; i++)
        {
            int columnIndex = Random.Range(0, availableColumns.Count);
            int x = availableColumns[columnIndex];
            availableColumns.RemoveAt(columnIndex);

            Transform treePrefab = treePrefabs[Random.Range(0, treePrefabs.Count)];
            Transform tree = Instantiate(treePrefab, transform);

            tree.position = new Vector3(x, TreeHeight, z);
            tree.Rotate(0f, YawAngles[Random.Range(0, YawAngles.Length)], 0f, Space.World);

            locations.Add(x);
        }

        return locations;
    }

    private void OnValidate()
    {
        spawnPowerupChance = Mathf.Clamp01(spawnPowerupChance);
    }

    private bool TrySpawnPowerup()
    {
        if (!CanRollPowerup() || Random.value >= spawnPowerupChance)
        {
            return false;
        }

        int prefabIndex = Random.Range(0, powerupPrefabs.Length);
        PowerupBase prefab = powerupPrefabs[prefabIndex];
        if (prefab == null)
        {
            Debug.LogWarning($"{nameof(Grass)} has an empty powerup prefab slot at index {prefabIndex}.", this);
            return false;
        }

        PowerupBase powerup = Instantiate(prefab, transform);
        powerup.transform.localPosition = new Vector3(PowerupLaneX, PowerupHeight + PowerupSpawnYOffset, 0f);
        return true;
    }

    private bool CanRollPowerup()
    {
        return spawnPowerupChance > 0f &&
               powerupPrefabs != null &&
               powerupPrefabs.Length > 0 &&
               !PowerupBase.HasLivePowerup &&
               !TehTarikPowerup.IsActive &&
               !HandPowerup.IsActive;
    }

    private List<int> BuildAvailableColumns()
    {
        List<int> columns = new();

        for (int x = playableAreaMinX + 1; x < playableAreaMaxX; x++)
        {
            columns.Add(x);
        }

        return columns;
    }
}
