using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RoadPotholeSpawner : MonoBehaviour
{
    private const string DefaultSpriteResourcePath = "Potholes/pothole";
    private const float MinDiameter = 0.01f;
    private const float FullCircleDegrees = 360f;
    private const float XzSurfacePitch = 90f;

    private enum SurfacePlane
    {
        XZ,
        XY
    }

    [Header("Sprite")]
    [SerializeField] private Sprite potholeSprite;
    [SerializeField] private string fallbackResourcePath = DefaultSpriteResourcePath;
    [SerializeField] private int sortingOrder = 50;
    [SerializeField] private Color potholeTint = new(0.05f, 0.045f, 0.04f, 1f);

    [Header("Spawn Count")]
    [SerializeField] private int minPotholes = 1;
    [SerializeField] private int maxPotholes = 4;

    [Header("Placement")]
    [SerializeField] private Transform boundsSource;
    [SerializeField] private SurfacePlane surfacePlane = SurfacePlane.XZ;
    [SerializeField] private Vector2 edgePadding = new(0.5f, 0.08f);
    [SerializeField] private float minSpacing = 1.25f;
    [SerializeField] private int maxPlacementAttempts = 40;
    [SerializeField] private float surfaceOffset = 0.06f;

    [Header("Size")]
    [SerializeField] private Vector2 diameterRange = new(0.9f, 1.45f);
    [SerializeField] private bool randomRotation = true;

    [Header("Runtime")]
    [SerializeField] private bool spawnOnStart = false;

    private readonly List<GameObject> spawnedPotholes = new();

    private void Start()
    {
        if (spawnOnStart)
        {
            RefreshPotholes();
        }
    }

    private void OnValidate()
    {
        minPotholes = Mathf.Max(0, minPotholes);
        maxPotholes = Mathf.Max(minPotholes, maxPotholes);
        minSpacing = Mathf.Max(0f, minSpacing);
        maxPlacementAttempts = Mathf.Max(1, maxPlacementAttempts);
        surfaceOffset = Mathf.Max(0f, surfaceOffset);

        edgePadding.x = Mathf.Max(0f, edgePadding.x);
        edgePadding.y = Mathf.Max(0f, edgePadding.y);

        diameterRange.x = Mathf.Max(MinDiameter, diameterRange.x);
        diameterRange.y = Mathf.Max(diameterRange.x, diameterRange.y);
    }

    public void RefreshPotholes()
    {
        ClearPotholes();

        Sprite sprite = ResolvePotholeSprite();
        if (sprite == null)
        {
            Debug.LogWarning($"{nameof(RoadPotholeSpawner)} has no pothole sprite assigned.", this);
            return;
        }

        if (!TryGetSpawnBounds(out Bounds bounds))
        {
            Debug.LogWarning($"{nameof(RoadPotholeSpawner)} could not find Renderer, Collider, or Collider2D bounds.", this);
            return;
        }

        int count = Random.Range(minPotholes, maxPotholes + 1);
        List<Vector2> acceptedPositions = new(count);

        for (int i = 0; i < count; i++)
        {
            if (!TryFindPosition(bounds, acceptedPositions, out Vector3 worldPosition, out Vector2 planarPosition))
            {
                continue;
            }

            acceptedPositions.Add(planarPosition);
            SpawnPothole(sprite, worldPosition);
        }
    }

    public void ClearPotholes()
    {
        for (int i = spawnedPotholes.Count - 1; i >= 0; i--)
        {
            GameObject pothole = spawnedPotholes[i];
            if (pothole == null)
            {
                continue;
            }

            if (Application.isPlaying)
            {
                Destroy(pothole);
            }
            else
            {
                DestroyImmediate(pothole);
            }
        }

        spawnedPotholes.Clear();
    }

    private Sprite ResolvePotholeSprite()
    {
        if (potholeSprite != null)
        {
            return potholeSprite;
        }

        return string.IsNullOrWhiteSpace(fallbackResourcePath)
            ? null
            : Resources.Load<Sprite>(fallbackResourcePath);
    }

    private bool TryGetSpawnBounds(out Bounds bounds)
    {
        Transform source = ResolveBoundsSource();

        if (source.TryGetComponent(out Renderer targetRenderer))
        {
            bounds = targetRenderer.bounds;
            return true;
        }

        if (source.TryGetComponent(out Collider targetCollider))
        {
            bounds = targetCollider.bounds;
            return true;
        }

        if (source.TryGetComponent(out Collider2D targetCollider2D))
        {
            bounds = targetCollider2D.bounds;
            return true;
        }

        bounds = default;
        return false;
    }

    private Transform ResolveBoundsSource()
    {
        if (boundsSource != null)
        {
            return boundsSource;
        }

        Transform playableSurface = transform.Find("Playable");
        return playableSurface != null ? playableSurface : transform;
    }

    private bool TryFindPosition(Bounds bounds, IReadOnlyList<Vector2> acceptedPositions, out Vector3 worldPosition, out Vector2 planarPosition)
    {
        float minA;
        float maxA;
        float minB;
        float maxB;

        if (surfacePlane == SurfacePlane.XZ)
        {
            minA = bounds.min.x + edgePadding.x;
            maxA = bounds.max.x - edgePadding.x;
            minB = bounds.min.z + edgePadding.y;
            maxB = bounds.max.z - edgePadding.y;
        }
        else
        {
            minA = bounds.min.x + edgePadding.x;
            maxA = bounds.max.x - edgePadding.x;
            minB = bounds.min.y + edgePadding.y;
            maxB = bounds.max.y - edgePadding.y;
        }

        if (minA > maxA || minB > maxB)
        {
            worldPosition = default;
            planarPosition = default;
            return false;
        }

        for (int attempt = 0; attempt < maxPlacementAttempts; attempt++)
        {
            planarPosition = new Vector2(Random.Range(minA, maxA), Random.Range(minB, maxB));
            if (IsTooClose(planarPosition, acceptedPositions))
            {
                continue;
            }

            worldPosition = surfacePlane == SurfacePlane.XZ
                ? new Vector3(planarPosition.x, bounds.max.y + surfaceOffset, planarPosition.y)
                : new Vector3(planarPosition.x, planarPosition.y, bounds.min.z - surfaceOffset);

            return true;
        }

        worldPosition = default;
        planarPosition = default;
        return false;
    }

    private bool IsTooClose(Vector2 candidate, IReadOnlyList<Vector2> acceptedPositions)
    {
        float minSpacingSqr = minSpacing * minSpacing;

        for (int i = 0; i < acceptedPositions.Count; i++)
        {
            if ((candidate - acceptedPositions[i]).sqrMagnitude < minSpacingSqr)
            {
                return true;
            }
        }

        return false;
    }

    private void SpawnPothole(Sprite sprite, Vector3 worldPosition)
    {
        GameObject pothole = new("Pothole");
        pothole.transform.position = worldPosition;
        pothole.transform.rotation = GetSurfaceRotation();

        SpriteRenderer spriteRenderer = pothole.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = sortingOrder;
        spriteRenderer.color = potholeTint;

        float targetDiameter = Random.Range(diameterRange.x, diameterRange.y);
        float spriteDiameter = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
        if (spriteDiameter > 0f)
        {
            float scale = targetDiameter / spriteDiameter;
            pothole.transform.localScale = new Vector3(scale, scale, scale);
        }

        pothole.transform.SetParent(transform, true);
        spawnedPotholes.Add(pothole);
    }

    private Quaternion GetSurfaceRotation()
    {
        float spin = randomRotation ? Random.Range(0f, FullCircleDegrees) : 0f;

        return surfacePlane == SurfacePlane.XZ
            ? Quaternion.Euler(XzSurfacePitch, 0f, spin)
            : Quaternion.Euler(0f, 0f, spin);
    }
}
