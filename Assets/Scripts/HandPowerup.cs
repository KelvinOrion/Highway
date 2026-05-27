using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class HandPowerup : PowerupBase
{
    private const string VehicleTag = "Vehicle";
    private const string CountdownAudioObjectName = "HandCountdownTickAudio";
    private const float PlayerFlashDuration = 0.2f;
    private const float CountdownTickPlaybackSeconds = 0.5f;
    private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorId = Shader.PropertyToID("_Color");

    [SerializeField] private float duration = 5f;
    [SerializeField] private AudioClip countdownTick;

    private static readonly Dictionary<Rigidbody, StoredVelocity> FrozenVehicles = new();
    private static Coroutine activeRoutine;
    private static MonoBehaviour activeHost;
    private static Coroutine countdownStopRoutine;
    private static MonoBehaviour countdownStopHost;
    private static AudioSource countdownAudioSource;

    public static bool IsActive { get; private set; }

    public static void ResetRuntimeState()
    {
        if (activeRoutine != null && activeHost != null)
        {
            activeHost.StopCoroutine(activeRoutine);
        }

        CleanupEffect();
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

        if (activeRoutine != null && activeHost != null)
        {
            activeHost.StopCoroutine(activeRoutine);
            CleanupEffect();
        }

        CacheAndFreezeActiveVehicles();
        manager.StartCoroutine(FlashPlayer(player, PlayerFlashDuration));

        activeHost = manager;
        IsActive = true;
        activeRoutine = manager.StartCoroutine(FreezeForDuration(Mathf.Max(0f, duration), player, countdownTick, manager));
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

    private static IEnumerator FreezeForDuration(float seconds, GameObject player, AudioClip countdownTick, MonoBehaviour host)
    {
        int tickCount = seconds >= 3f ? 3 : Mathf.FloorToInt(seconds);
        float preTickDelay = Mathf.Max(0f, seconds - tickCount);

        if (preTickDelay > 0f)
        {
            yield return new WaitForSeconds(preTickDelay);
        }

        for (int i = 0; i < tickCount; i++)
        {
            PlayCountdownTick(player, countdownTick, host);
            yield return new WaitForSeconds(1f);
        }

        CleanupEffect();
        IsActive = false;
        activeRoutine = null;
        activeHost = null;
    }

    private static void PlayCountdownTick(GameObject player, AudioClip countdownTick, MonoBehaviour host)
    {
        if (player == null || countdownTick == null || host == null)
        {
            return;
        }

        AudioSource source = ResolveCountdownSource(player);
        if (source == null)
        {
            return;
        }

        StopCountdownAudio();

        source.clip = countdownTick;
        source.loop = false;
        source.Play();

        countdownAudioSource = source;
        countdownStopHost = host;
        countdownStopRoutine = host.StartCoroutine(StopCountdownTickAfterDelay(source, CountdownTickPlaybackSeconds));
    }

    private static IEnumerator StopCountdownTickAfterDelay(AudioSource source, float seconds)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (source != null)
        {
            source.Stop();
        }

        if (countdownAudioSource == source)
        {
            countdownAudioSource = null;
            countdownStopRoutine = null;
            countdownStopHost = null;
        }
    }

    private static AudioSource ResolveCountdownSource(GameObject player)
    {
        if (countdownAudioSource != null)
        {
            return countdownAudioSource;
        }

        Transform audioTransform = player.transform.Find(CountdownAudioObjectName);
        AudioSource source = audioTransform != null ? audioTransform.GetComponent<AudioSource>() : null;
        if (source == null)
        {
            GameObject audioObject = new(CountdownAudioObjectName);
            audioObject.transform.SetParent(player.transform, false);
            source = audioObject.AddComponent<AudioSource>();
        }

        source.playOnAwake = false;
        source.loop = false;
        source.spatialBlend = 0f;
        countdownAudioSource = source;
        return source;
    }

    private static void CleanupEffect()
    {
        StopCountdownAudio();
        RestoreFrozenVehicles();
    }

    private static void StopCountdownAudio()
    {
        if (countdownStopRoutine != null && countdownStopHost != null)
        {
            countdownStopHost.StopCoroutine(countdownStopRoutine);
        }

        if (countdownAudioSource != null)
        {
            countdownAudioSource.Stop();
        }

        countdownStopRoutine = null;
        countdownStopHost = null;
        countdownAudioSource = null;
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
