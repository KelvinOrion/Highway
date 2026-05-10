using UnityEngine;

public interface IPlayerInput
{
    event System.Action OnMoveForward;
    event System.Action OnMoveBack;
    event System.Action OnMoveLeft;
    event System.Action OnMoveRight;
}

public static class InputDirectionUtility
{
    public enum SwipeDirection
    {
        Forward,
        Back,
        Left,
        Right
    }

    // Converts a drag delta into the game's four cardinal movement commands.
    public static SwipeDirection GetSwipeDirection(Vector2 delta, float swipeThreshold)
    {
        if (delta.magnitude < swipeThreshold)
        {
            return SwipeDirection.Forward;
        }

        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
        {
            return delta.x > 0f ? SwipeDirection.Right : SwipeDirection.Left;
        }

        return delta.y > 0f ? SwipeDirection.Forward : SwipeDirection.Back;
    }
}
