using System.Collections;
using UnityEngine;

public class Character : MonoBehaviour
{
    private const float VisualYawOffset = 90f;
    private const int ImpactPopPieceCount = 9;
    private const float ImpactPopDuration = 0.65f;
    private const float ImpactPopRadius = 0.75f;
    private const float ImpactPopHeightOffset = 0.1f;
    private const float ImpactPopShardLift = 0.45f;
    private const float ImpactPopCenterRadiusScale = 0.35f;
    private const float ImpactPopCenterScale = 0.22f;
    private const float ImpactPopShardWidth = 0.08f;
    private const float ImpactPopShardLength = 0.26f;
    private const float ImpactPopEasePower = 3f;
    private const float ImpactPopScaleHalfWave = 0.5f;
    private const float ImpactPopSpinDegreesPerSecond = 540f;

    private static readonly Color[] ImpactPopColors =
    {
        new(1f, 0.92f, 0.18f),
        new(1f, 0.48f, 0.08f),
        new(1f, 0.18f, 0.08f),
        new(1f, 0.98f, 0.72f)
    };

    [SerializeField] private GameManager gameManager;
    [SerializeField] private GameObject character;
    [SerializeField] private ParticleSystem deathParticles;
    [SerializeField] private AudioSource deathSound;

    private Rigidbody characterBody;
    private Collider characterCollider;
    private Renderer[] characterRenderers;
    private Coroutine impactPopRoutine;
    private GameObject impactPopRoot;
    private bool isDead;

    private void Awake()
    {
        characterBody = GetComponent<Rigidbody>();
        characterCollider = GetComponent<Collider>();
        characterRenderers = GetComponentsInChildren<Renderer>(true);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (isDead || !collision.gameObject.CompareTag("Vehicle") || character == null || !character.activeSelf)
        {
            return;
        }

        Vector3 collisionPoint = collision.contactCount > 0
            ? collision.GetContact(0).point
            : collision.transform.position;

        Kill(collisionPoint);
    }

    /// <summary>
    /// Faces the whole player root toward the grid direction the player just moved.
    /// Rotating the root matches the reference project and keeps prefab child models aligned.
    /// </summary>
    public void FaceDirection(Vector2Int direction)
    {
        if (direction == Vector2Int.zero)
        {
            return;
        }

        SetFacing(DirectionToYaw(direction));
    }

    private static float DirectionToYaw(Vector2Int direction)
    {
        if (direction == Vector2Int.right) return 90f;
        if (direction == Vector2Int.down) return 180f;
        if (direction == Vector2Int.left) return 270f;

        return 0f;
    }

    public void Kill(Vector3 collisionPoint)
    {
        isDead = true;
        PlayImpactPop(collisionPoint);
        SetCharacterVisible(false);

        if (deathParticles != null)
        {
            deathParticles.transform.position = collisionPoint;
            deathParticles.transform.LookAt(transform.position + Vector3.up);
            deathParticles.Play(true);
        }

        deathSound?.Play();

        gameManager?.PlayerCollision();
    }

    public void Reset()
    {
        isDead = false;

        if (character != null)
        {
            character.SetActive(true);
        }

        SetCharacterVisible(true);
        SetFacing(DirectionToYaw(Vector2Int.up));

        if (deathParticles != null)
        {
            deathParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            deathParticles.Clear();
        }

        if (deathSound != null)
        {
            deathSound.Stop();
        }

        ClearImpactPop(true);
    }

    private void SetFacing(float yaw)
    {
        Quaternion rotation = Quaternion.Euler(0f, yaw + VisualYawOffset, 0f);
        transform.localRotation = rotation;

        if (characterBody != null)
        {
            characterBody.rotation = transform.rotation;
        }
    }

    private void SetCharacterVisible(bool visible)
    {
        if (characterRenderers != null)
        {
            foreach (Renderer characterRenderer in characterRenderers)
            {
                if (characterRenderer == null || characterRenderer.GetComponentInParent<ParticleSystem>() != null)
                {
                    continue;
                }

                characterRenderer.enabled = visible;
            }
        }

        if (characterCollider != null)
        {
            characterCollider.enabled = visible;
        }
    }

    private void PlayImpactPop(Vector3 collisionPoint)
    {
        ClearImpactPop(true);

        impactPopRoot = new GameObject("ComicImpactPop");
        impactPopRoot.transform.position = collisionPoint + Vector3.up * ImpactPopHeightOffset;
        impactPopRoutine = StartCoroutine(AnimateImpactPop(impactPopRoot.transform));
    }

    // Builds a short-lived comic impact burst from primitive meshes.
    private IEnumerator AnimateImpactPop(Transform root)
    {
        Transform[] pieces = new Transform[ImpactPopPieceCount];
        Vector3[] startPositions = new Vector3[pieces.Length];
        Vector3[] endPositions = new Vector3[pieces.Length];
        Vector3[] baseScales = new Vector3[pieces.Length];

        for (int i = 0; i < pieces.Length; i++)
        {
            bool isCenter = i == 0;
            GameObject piece = GameObject.CreatePrimitive(isCenter ? PrimitiveType.Sphere : PrimitiveType.Cube);
            piece.name = isCenter ? "PopCenter" : $"PopShard_{i}";
            piece.transform.SetParent(root, false);

            Collider pieceCollider = piece.GetComponent<Collider>();
            if (pieceCollider != null)
            {
                Destroy(pieceCollider);
            }

            float angle = i / (float)(pieces.Length - 1) * Mathf.PI * 2f;
            Vector3 direction = isCenter
                ? Vector3.up
                : new Vector3(Mathf.Cos(angle), ImpactPopShardLift, Mathf.Sin(angle)).normalized;

            pieces[i] = piece.transform;
            startPositions[i] = Vector3.zero;
            endPositions[i] = direction * (isCenter ? ImpactPopRadius * ImpactPopCenterRadiusScale : ImpactPopRadius);
            baseScales[i] = isCenter
                ? Vector3.one * ImpactPopCenterScale
                : new Vector3(ImpactPopShardWidth, ImpactPopShardWidth, ImpactPopShardLength);
            piece.transform.localScale = Vector3.zero;
            piece.transform.localRotation = isCenter ? Quaternion.identity : Quaternion.LookRotation(direction, Vector3.up);

            Renderer pieceRenderer = piece.GetComponent<Renderer>();
            if (pieceRenderer != null)
            {
                pieceRenderer.material.color = ImpactPopColors[i % ImpactPopColors.Length];
            }
        }

        float elapsed = 0f;
        while (elapsed < ImpactPopDuration && root != null)
        {
            float t = Mathf.Clamp01(elapsed / ImpactPopDuration);
            float outEase = 1f - Mathf.Pow(1f - t, ImpactPopEasePower);
            float scale = Mathf.Sin(Mathf.Clamp01(1f - t) * Mathf.PI * ImpactPopScaleHalfWave);

            for (int i = 0; i < pieces.Length; i++)
            {
                if (pieces[i] == null)
                {
                    continue;
                }

                pieces[i].localPosition = Vector3.Lerp(startPositions[i], endPositions[i], outEase);
                pieces[i].localScale = baseScales[i] * scale;
                pieces[i].Rotate(Vector3.up, ImpactPopSpinDegreesPerSecond * Time.deltaTime, Space.Self);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        ClearImpactPop(false);
    }

    private void ClearImpactPop(bool stopRoutine)
    {
        if (stopRoutine && impactPopRoutine != null)
        {
            StopCoroutine(impactPopRoutine);
        }

        impactPopRoutine = null;

        if (impactPopRoot != null)
        {
            Destroy(impactPopRoot);
            impactPopRoot = null;
        }
    }
}
