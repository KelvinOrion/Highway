using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class GameManager : MonoBehaviour
{
    [Header("Game objects")]
    [SerializeField] private Transform character;
    [SerializeField] private Transform characterModel;
    [SerializeField] private Transform terrainHolder;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private int spawnDistance = 20;

    //6 references
    enum GameState
    {
        Ready,
        Moving,
        Dead
    }
    private GameState gameState;
    private Vector2Int characterPos;
    private int spawnLocation;
    private List<(float terrainHeight, HashSet<int> locations)> obstacles = new();

    void Awake()
    {
        //Initialise all the starting state/
        NewLevel();
    }

    private void NewLevel()
    {
        gameState = GameState.Ready;

        //Reset character position every new round
        characterPos = new Vector2Int(0, -1);
        character.position = new Vector3(0, 0.2f, -1);

        //Remove all terrain
        obstacles.Clear();
        foreach (Transform child in terrainHolder)
        {
            Destroy(child.gameObject);
        }

        //Reset level, regenerate
        spawnLocation = 0;
        for (int i = 0; i < spawnDistance; i++)
        {
            SpawnObstacles();
        }
    }

    private void SpawnObstacles()
    {
        //Create grass with terrain height of 0.2f
        Grass grass = Instantiate(grassPrefab, terrainHolder);
        obstacles.Add((0.2f, grass.Init(spawnLocation)));

        //Update to the next location
        spawnLocation++;

    }

    private bool InStartArea(Vector2Int location)
    {
        //Movement anywhere in the starting region is aligned.
        if ((location.y > -5) && (location.y < 0) && (location.x > -6) && (location.x < 6)) { return true; }
        return false;
    }

    private IEnumerator MoveCharacter()
    {
        gameState = GameState.Moving;
        float elapsedTime = 0f;

        //The yHeight changes if we're on grass or road
        float yHeight = 0.2f;

        Vector3 startPos = character.position;
        Vector3 endPos = new(characterPos.x, yHeight, characterPos.y);

        while (elapsedTime < moveDuration)
        {
            //How far through the animation are we
            float percent = elapsedTime / moveDuration;

            //Update character position
            Vector3 newPos = Vector3.Lerp(startPos, endPos, percent);

            //Make character jump in an arc
            newPos.y = yHeight = (0.5f * Mathf.Sin(Mathf.PI * percent));
            character.position = newPos;

            //Update elapsed time
            elapsedTime += Time.deltaTime;

            yield return null;
        }

        //Ensure we're at the end
        character.position = endPos;

        //Need to check we're still in moving at the end.
        //If we're dead we don't want to go back to ready.
        if (gameState == GameState.Moving)
        {
            gameState = GameState.Ready;
        }
    }

    // function to move character, and build for independent from platform
    private void TryMove(Vector2Int direction)
    {
        // Ignore empty intent
        if (direction == Vector2Int.zero) return;

        // Only move when ready
        if (gameState != GameState.Ready) return;

        Vector2Int destination = characterPos + direction;

        // In v0.1 we only check start area
        if (InStartArea(destination) || ((destination.y >= 0) && !obstacles[destination.y].locations.Contains(destination.x)))
        {
            {
                characterPos = destination;
                StartCoroutine(MoveCharacter());
            }

            // Spawn new obstacles if necessary
            while (obstacles.Count < (characterPos.y + spawnDistance))
            {
                SpawnObstacles();
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        Vector2Int moveDirection = Vector2Int.zero;

        if (Keyboard.current.upArrowKey.isPressed)
        {
            character.localRotation = Quaternion.identity;
            moveDirection = Vector2Int.up;
        }
        else if (Keyboard.current.downArrowKey.isPressed)
        {
            character.localRotation = Quaternion.Euler(0, 180, 0);
            moveDirection = Vector2Int.down;
        }
        else if (Keyboard.current.leftArrowKey.isPressed)
        {
            character.localRotation = Quaternion.Euler(0, -90, 0);
            moveDirection = Vector2Int.left;
        }
        else if (Keyboard.current.rightArrowKey.isPressed)
        {
            character.localRotation = Quaternion.Euler(0, 90, 0);
            moveDirection = Vector2Int.right;
        }

        TryMove(moveDirection);
    }

}
