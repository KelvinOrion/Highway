using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HandPowerup : PowerupBase
{
    private const string VehicleTag = "Vehicle";
    private const float PlayerFlashDuration = 0.2f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private float duration = 5f;
    [SerializeField] private AudioClip countdownTick;

    private static readonly Dictionary<Rigidbody, StoredVelocity> FrozenVehicles = new();
    private static Coroutine activeRoutine;
    private static MonoBehaviour activeHost;

    public static bool IsActive { get; private set; }

    public static void ResetRuntimeState()
    {
        if (activeRoutine != null && activeHost != null)
        {
            activeHost.StopCoroutine(activeRoutine);
        }

        RestoreFrozenVehicles();
        IsActive = false;
        activeRoutine = null;
        activeHost = null;
    }

    protected override void Activate(GameObject player)
    {
        GameManager manager = FindFirstObjectByType<GameManager>();
        if (manager == null)
        {
            Debug.LogWarning($"{nameof(HandPowerup)} could not find {nameof(GameManager)} to run the freeze timer.", this);
            return;
        }

        CacheAndFreezeActiveVehicles();
        manager.StartCoroutine(FlashPlayer(player, PlayerFlashDuration));

        if (activeRoutine != null && activeHost != null)
        {
            activeHost.StopCoroutine(activeRoutine);
        }

        activeHost = manager;
        IsActive = true;
        activeRoutine = manager.StartCoroutine(FreezeForDuration(Mathf.Max(0f, duration), player, countdownTick));
    }

    public static void FreezeVehicle(Rigidbody vehicle)
    {
        if (vehicle == null || IsMak(vehicle.gameObject))
        {
            return;
        }

        if (!FrozenVehicles.ContainsKey(vehicle))
        {
            FrozenVehicles.Add(vehicle, new StoredVelocity(vehicle));
        }

        ApplyFrozenVelocity(vehicle);
    }

    private static void CacheAndFreezeActiveVehicles()
    {
        foreach (GameObject vehicleObject in GameObject.FindGameObjectsWithTag(VehicleTag))
        {
            if (IsMak(vehicleObject))
            {
                continue;
            }

            Rigidbody body = vehicleObject.GetComponent<Rigidbody>();
            if (body == null)
            {
                body = vehicleObject.GetComponentInChildren<Rigidbody>();
            }

            FreezeVehicle(body);
        }
    }

    private static IEnumerator FreezeForDuration(float seconds, GameObject player, AudioClip countdownTick)
    {
        int tickCount = seconds >= 3f ? 3 : Mathf.FloorToInt(seconds);
        float preTickDelay = Mathf.Max(0f, seconds - tickCount);

        if (preTickDelay > 0f)
        {
            yield return new WaitForSeconds(preTickDelay);
        }

        for (int i = 0; i < tickCount; i++)
        {
            PlayCountdownTick(player, countdownTick);
            yield return new WaitForSeconds(1f);
        }

        RestoreFrozenVehicles();
        IsActive = false;
        activeRoutine = null;
        activeHost = null;
    }

    private static void PlayCountdownTick(GameObject player, AudioClip countdownTick)
    {
        if (player == null || countdownTick == null)
        {
            return;
        }

        AudioSource source = player.GetComponent<AudioSource>();
        if (source == null)
        {
            source = player.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f;
        }

        source.PlayOneShot(countdownTick);
    }

    private static void RestoreFrozenVehicles()
    {
        foreach (StoredVelocity storedVelocity in FrozenVehicles.Values)
        {
            storedVelocity.Restore();
        }

        FrozenVehicles.Clear();
    }

    private static IEnumerator FlashPlayer(GameObject player, float seconds)
    {
        if (player == null)
        {
            yield break;
        }

        List<MaterialColorState> originalColors = new();
        foreach (Renderer playerRenderer in player.GetComponentsInChildren<Renderer>(true))
        {
            if (playerRenderer == null || playerRenderer.GetComponentInParent<ParticleSystem>() != null)
            {
                continue;
            }

            foreach (Material material in playerRenderer.materials)
            {
                if (material == null || !TryGetColorProperty(material, out int colorProperty))
                {
                    continue;
                }

                originalColors.Add(new MaterialColorState(material, colorProperty, material.GetColor(colorProperty)));
                material.SetColor(colorProperty, Color.white);
            }
        }

        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        foreach (MaterialColorState originalColor in originalColors)
        {
            originalColor.Restore();
        }
    }

    private static void ApplyFrozenVelocity(Rigidbody body)
    {
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
    }

    private static bool IsMak(GameObject candidate)
    {
        if (candidate == null)
        {
            return false;
        }

        foreach (Component component in candidate.GetComponentsInParent<Component>(true))
        {
            if (component != null && component.GetType().Name == "MakController")
            {
                return true;
            }
        }

        return false;
    }

    private readonly struct StoredVelocity
    {
        private readonly Rigidbody body;
        private readonly Vector3 linearVelocity;
        private readonly Vector3 angularVelocity;

        public StoredVelocity(Rigidbody body)
        {
            this.body = body;
            linearVelocity = body != null && !body.isKinematic ? body.linearVelocity : Vector3.zero;
            angularVelocity = body != null && !body.isKinematic ? body.angularVelocity : Vector3.zero;
        }

        public void Restore()
        {
            if (body == null || body.isKinematic)
            {
                return;
            }

            body.linearVelocity = linearVelocity;
            body.angularVelocity = angularVelocity;
        }
    }

    private readonly struct MaterialColorState
    {
        private readonly Material material;
        private readonly int colorProperty;
        private readonly Color color;

        public MaterialColorState(Material material, int colorProperty, Color color)
        {
            this.material = material;
            this.colorProperty = colorProperty;
            this.color = color;
        }

        public void Restore()
        {
            if (material != null)
            {
                material.SetColor(colorProperty, color);
            }
        }
    }

    private static bool TryGetColorProperty(Material material, out int colorProperty)
    {
        if (material.HasProperty(BaseColorId))
        {
            colorProperty = BaseColorId;
            return true;
        }

        if (material.HasProperty(ColorId))
        {
            colorProperty = ColorId;
            return true;
        }

        colorProperty = 0;
        return false;
    }
}
