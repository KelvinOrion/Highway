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
    [SerializeField] private TMPro.TextMeshProUGUI scoreText;

    [Header("Terrain objects")]
    [SerializeField] private Grass grassPrefab;
    [SerializeField] private Road roadPrefab;

    [Header("Game parameters")]
    [SerializeField] private float moveDuration = 0.2f;
    [SerializeField] private int spawnDistance = 20;
    [SerializeField] private float shakeDuration = 0.15f;
    [SerializeField] private float shakeMagnitude = 0.15f;

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
    private List<(float terrainHeight, HashSet<int> locations, GameObject obj)> obstacles = new();
    private int score = 0;
    private Vector3 cameraBasePos;

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
        character.GetComponent<Character>().Reset();

        // Reset score 
        score = 0;
        scoreText.text = "0";
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
        //slowing increment the road spawn rate
        float roadProbability = Mathf.Lerp(0.5f, 0.9f, spawnLocation / 250f);

        if (Random.value < roadProbability)
        {
            //create road with terrain height of 0.1f
            Road road = Instantiate(roadPrefab, terrainHolder);
            obstacles.Add((0.1f, road.Init(spawnLocation), road.gameObject));
            road.gameObject.name = $"{spawnLocation} - Road";
        }
        else
        {
            //Create grass with terrain height of 0.2f
            Grass grass = Instantiate(grassPrefab, terrainHolder);
            obstacles.Add((0.2f, grass.Init(spawnLocation), grass.gameObject));
            grass.gameObject.name = $"{spawnLocation} - Grass";
        }

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
        if (characterPos.y >= 0)
        {
            yHeight = obstacles[characterPos.y].terrainHeight;
        }

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

        // move area
        if (InStartArea(destination) || ((destination.y >= 0) && !obstacles[destination.y].locations.Contains(destination.x)))
        {
            {
                characterPos = destination;
                StartCoroutine(MoveCharacter());
                // Update score if we moved forward
                if ((destination.y + 1) > score)
                {
                    score = destination.y + 1;
                    scoreText.text = $"{score}";
                }
            }

            // Spawn new obstacles if necessary
            while (obstacles.Count < (characterPos.y + spawnDistance))
            {
                SpawnObstacles();

                //Destroy old obstacles to save memory
                int oldIndex = characterPos.y - spawnDistance;
                if ((oldIndex >= 0) && (obstacles[oldIndex].obj != null))
                {
                    Destroy(obstacles[oldIndex].obj);
                }
            }

            // If character gone too far back, end game
            if (characterPos.y < (score - 10))
            {
                character.GetComponent<Character>().Kill(character.position + new Vector3(0, 0.2f, 0.5f));
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

        // Can only use shortcut to restart when dead
        if (gameState == GameState.Dead && Keyboard.current.spaceKey.wasPressedThisFrame)
        {
            NewLevel();
        }

        //  Camera follows at (4,7,-5)
        Vector3 cameraPosition = new(character.position.x +4, 7,character.position.z -5);

        //Limit camera movement in x directions.
        // Only follow the characteras it moves to -3 and +3.
        //The camera is offset +2 so that 2 to 7 in the camera x position.
        cameraPosition.x = Mathf.Clamp(cameraPosition.x, 2, 7);

        Camera.main.transform.position = cameraPosition;
        cameraBasePos = Camera.main.transform.position;

    }

    public void PlayerCollision()
    {
        //Set game state to dead
        gameState = GameState.Dead;
        StartCoroutine(ScreenShake());
        ////Disable character model
        //characterModel.gameObject.SetActive(false);
        ////Restart level after a delay
        //Invoke(nameof(NewLevel), 1.0f);
    }

    private IEnumerator ScreenShake()
    {
        float elapsed = 0f;

        while (elapsed < shakeDuration)
        {
            Vector3 offset = Random.insideUnitSphere * shakeMagnitude;
            Camera.main.transform.position = cameraBasePos + offset;

            elapsed += Time.unscaledDeltaTime;
            yield return null;
        }

        Camera.main.transform.position = cameraBasePos;
    }

}
