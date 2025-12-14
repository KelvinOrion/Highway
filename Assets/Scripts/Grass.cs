using System.Collections.Generic;
using UnityEngine;

public class Grass : MonoBehaviour
{
    [SerializeField] private Transform treePrefab;

    public HashSet<int> Init(float z)
    {
        // Place the obstacle at the location provided.
        transform.position = new Vector3(0, 0, z);

        // We always have obstacles outside the game area.
        HashSet<int> locations = new() { -6, 6 };

        // Populate with some obstacles
        int numTrees = Random.Range(1, 5);

        for (int i = 0; i < numTrees; i++)
        {
            // Create a new tree object
            Transform tree = Instantiate(treePrefab, transform);

            // Put it in a random position
            int xPos = Random.Range(-5, 6);
            tree.position = new Vector3(xPos, 0.2f, z);

            // Record the location in our HashSet.
            locations.Add(xPos);
        }

        return locations;
    }
}
