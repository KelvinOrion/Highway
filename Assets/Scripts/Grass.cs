using System.Collections.Generic;
using UnityEngine;

public class Grass : MonoBehaviour
{
    private const int MinTreeCount = 1;
    private const int MaxTreeCountExclusive = 5;
    private const float TreeHeight = 0.2f;

    [SerializeField] private List<Transform> treePrefabs;

    // These bounds must stay aligned with GameManager's playable x range.
    [SerializeField] private int playableAreaMinX = -10;
    [SerializeField] private int playableAreaMaxX = 10;

    private static readonly int[] YawAngles = { 0, 90, 180, 270 };

    public HashSet<int> Init(float z)
    {
        transform.position = new Vector3(0, 0, z);

        HashSet<int> locations = new() { playableAreaMinX, playableAreaMaxX };

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
