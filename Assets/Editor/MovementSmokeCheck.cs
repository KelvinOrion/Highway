#if UNITY_EDITOR
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public static class MovementSmokeCheck
{
    private const string ScenePath = "Assets/Scenes/MobileScene.unity";
    private const string PendingKey = "Highway.MovementSmokeCheck.Pending";
    private const int MinimumPlayFrames = 2;
    private const float MoveWaitSeconds = 0.35f;
    private const float ExpectedForwardMove = 0.9f;
    private const int SuccessExitCode = 0;
    private const int FailureExitCode = 1;

    private static int playFrames;
    private static bool moveInvoked;
    private static float startZ;
    private static float moveStartedAt;

    [InitializeOnLoadMethod]
    private static void ResumeAfterDomainReload()
    {
        if (SessionState.GetBool(PendingKey, false))
        {
            EditorApplication.update -= Tick;
            EditorApplication.update += Tick;
        }
    }

    public static void Run()
    {
        SessionState.SetBool(PendingKey, true);
        ResetState();
        EditorSceneManager.OpenScene(ScenePath);
        EditorApplication.update += Tick;
        EditorApplication.EnterPlaymode();
    }

    private static void Tick()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        playFrames++;
        if (playFrames < MinimumPlayFrames)
        {
            return;
        }

        GameManager gameManager = Object.FindFirstObjectByType<GameManager>();
        Transform character = GameObject.Find("Character_Root")?.transform;

        if (gameManager == null || character == null)
        {
            Fail("Could not find GameManager or Characters in MobileScene.");
            return;
        }

        if (!moveInvoked)
        {
            startZ = character.position.z;
            moveStartedAt = Time.realtimeSinceStartup;
            InvokeMove(gameManager, Vector2Int.up);
            moveInvoked = true;
            return;
        }

        if (Time.realtimeSinceStartup - moveStartedAt < MoveWaitSeconds)
        {
            return;
        }

        if (character.position.z > startZ + ExpectedForwardMove)
        {
            Debug.Log($"Movement smoke check passed. Z moved from {startZ} to {character.position.z}.");
            CleanupAndExit(SuccessExitCode);
        }
        else
        {
            Fail($"Expected character Z to advance by one row. Start: {startZ}, Current: {character.position.z}.");
        }
    }

    private static void InvokeMove(GameManager gameManager, Vector2Int direction)
    {
        MethodInfo tryMove = typeof(GameManager).GetMethod(
            "TryMove",
            BindingFlags.Instance | BindingFlags.NonPublic);

        if (tryMove == null)
        {
            Fail("GameManager.TryMove was not found.");
            return;
        }

        tryMove.Invoke(gameManager, new object[] { direction });
    }

    private static void Fail(string message)
    {
        Debug.LogError($"Movement smoke check failed: {message}");
        CleanupAndExit(FailureExitCode);
    }

    private static void CleanupAndExit(int exitCode)
    {
        SessionState.SetBool(PendingKey, false);
        EditorApplication.update -= Tick;
        EditorApplication.Exit(exitCode);
    }

    private static void ResetState()
    {
        playFrames = 0;
        moveInvoked = false;
        startZ = 0f;
        moveStartedAt = 0f;
    }
}
#endif
