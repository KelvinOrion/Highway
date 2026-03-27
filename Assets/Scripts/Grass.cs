using System.Collections.Generic;
using UnityEngine;

public class Grass : MonoBehaviour
{
    [SerializeField] private Transform treePrefab;
    
    // Playable area boundaries - match GameManager's InStartArea bounds
    // These should match the character's movement constraints
    [SerializeField] private int playableAreaMinX = -10;
    [SerializeField] private int playableAreaMaxX = 10;

    public HashSet<int> Init(float z)
    {
        // Place the obstacle at the location provided.
        transform.position = new Vector3(0, 0, z);

        // We always have obstacles at the edges of the playable area.
        HashSet<int> locations = new() { playableAreaMinX, playableAreaMaxX };

        // Populate with some obstacles within the playable area
        int numTrees = Random.Range(1, 5);

        for (int i = 0; i < numTrees; i++)
        {
            // Create a new tree object
            Transform tree = Instantiate(treePrefab, transform);

            // Put it in a random position within playable area (excluding edges)
            int xPos = Random.Range(playableAreaMinX + 1, playableAreaMaxX);
            tree.position = new Vector3(xPos, 0.2f, z);

            // Record the location in our HashSet.
            locations.Add(xPos);
        }

        return locations;
    }
}
