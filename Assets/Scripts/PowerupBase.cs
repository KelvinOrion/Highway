using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public abstract class PowerupBase : MonoBehaviour
{
    private const string DefaultPlayerTag = "Player";

    private static readonly HashSet<PowerupBase> LivePowerups = new();
    private static Sprite placeholderSprite;

    [SerializeField] private AudioClip collectSfx;
    [SerializeField] private float collectSfxVolume = 1f;
    [SerializeField] private string playerTag = DefaultPlayerTag;
    [SerializeField] private float bobAmplitude = 0.1f;
    [SerializeField] private float bobSpeed = 2f;
    [SerializeField] private float circularMotionRadius = 0.12f;
    [SerializeField] private float circularMotionSpeed = 2f;
    [SerializeField] private float rotationSpeed = 90f;
    [SerializeField] private Color placeholderTint = Color.white;

    private Vector3 baseLocalPosition;
    private Quaternion baseLocalRotation;
    private bool hasBaseLocalPosition;
    private bool hasBaseLocalRotation;
    private bool collected;

    public static bool HasLivePowerup
    {
        get
        {
            LivePowerups.RemoveWhere(powerup => powerup == null);
            return LivePowerups.Count > 0;
        }
    }

    public static void ClearLivePowerups()
    {
        LivePowerups.Clear();
    }

    private void Awake()
    {
        EnsureTriggerCollider();
        EnsurePlaceholderSprite();
    }

    private void OnEnable()
    {
        LivePowerups.Add(this);
    }

    private void OnDisable()
    {
        LivePowerups.Remove(this);
    }

    private void Start()
    {
        CaptureBaseLocalPosition();
        CaptureBaseLocalRotation();
    }

    private void Update()
    {
        if (!hasBaseLocalPosition)
        {
            CaptureBaseLocalPosition();
        }

        if (!hasBaseLocalRotation)
        {
            CaptureBaseLocalRotation();
        }

        float circularAngle = Time.time * circularMotionSpeed;
        float bobOffset = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        Vector3 circularOffset = new(
            Mathf.Cos(circularAngle) * circularMotionRadius,
            0f,
            Mathf.Sin(circularAngle) * circularMotionRadius);

        transform.localPosition = baseLocalPosition + Vector3.up * bobOffset + circularOffset;
        transform.localRotation = baseLocalRotation * Quaternion.Euler(0f, Time.time * rotationSpeed, 0f);
    }

    private void OnTriggerEnter(Collider other)
    {
        TryCollect(other.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        TryCollect(other.gameObject);
    }

    protected abstract void Activate(GameObject player);

    private void TryCollect(GameObject candidate)
    {
        if (collected || !IsPlayer(candidate))
        {
            return;
        }

        collected = true;
        PlayCollectSfx();
        Activate(candidate);
        Destroy(gameObject);
    }

    private bool IsPlayer(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        if (HasConfiguredPlayerTag(candidate))
        {
            return true;
        }

        return candidate.GetComponentInParent<Character>() != null;
    }

    private bool HasConfiguredPlayerTag(GameObject candidate)
    {
        if (string.IsNullOrWhiteSpace(playerTag))
        {
            return false;
        }

        try
        {
            return candidate.CompareTag(playerTag);
        }
        catch (UnityException)
        {
            return false;
        }
    }

    private void PlayCollectSfx()
    {
        if (collectSfx == null)
        {
            return;
        }

        AudioSource.PlayClipAtPoint(collectSfx, transform.position, Mathf.Clamp01(collectSfxVolume));
    }

    private void CaptureBaseLocalPosition()
    {
        baseLocalPosition = transform.localPosition;
        hasBaseLocalPosition = true;
    }

    private void CaptureBaseLocalRotation()
    {
        baseLocalRotation = transform.localRotation;
        hasBaseLocalRotation = true;
    }

    private void EnsureTriggerCollider()
    {
        foreach (Collider triggerCollider in GetComponents<Collider>())
        {
            triggerCollider.isTrigger = true;
        }

        foreach (Collider2D triggerCollider in GetComponents<Collider2D>())
        {
            triggerCollider.isTrigger = true;
        }
    }

    private void EnsurePlaceholderSprite()
    {
        SpriteRenderer spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            return;
        }

        spriteRenderer.color = placeholderTint;
        if (spriteRenderer.sprite != null)
        {
            return;
        }

        placeholderSprite ??= CreatePlaceholderSprite();
        spriteRenderer.sprite = placeholderSprite;
    }

    private static Sprite CreatePlaceholderSprite()
    {
        Texture2D texture = new(1, 1)
        {
            filterMode = FilterMode.Point
        };
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();

        return Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
    }
}
