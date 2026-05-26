using System.Collections;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class TehTarikPowerup : PowerupBase
{
    private const float MinSpeedMultiplier = 0.01f;
    private const float CameraShakeDuration = 0.3f;
    private const float CameraZoomMultiplier = 0.85f;
    private const float CameraZoomDuration = 0.3f;
    private const float SpeedLinesCameraDistance = 1f;
    private const float SpeedLinesDestroyDelay = 0.15f;

    [SerializeField] private float speedMultiplier = 1.6f;
    [SerializeField] private float duration = 5f;
    [SerializeField] private float shakeMagnitude = 0.05f;
    [SerializeField] private ParticleSystem speedLinesPrefab;

    private static Coroutine activeRoutine;
    private static Coroutine activeZoomRoutine;
    private static GameManager activeManager;
    private static MonoBehaviour activeZoomHost;
    private static Camera activeCamera;
    private static ParticleSystem activeSpeedLines;
    private static GameObject activeSpeedLinesObject;
    private static float originalMoveDuration;
    private static float originalCameraZoom;
    private static bool originalCameraWasOrthographic;
    private static bool hasOriginalCameraZoom;

    public static bool IsActive { get; private set; }

    public static void ResetRuntimeState()
    {
        if (activeRoutine != null && activeManager != null)
        {
            activeManager.StopCoroutine(activeRoutine);
        }

        StopActiveZoomRoutine();
        RestoreCameraZoomImmediate();
        StopSpeedLines(clearParticles: true);

        activeRoutine = null;
        activeManager = null;
        activeCamera = null;
        originalCameraZoom = 0f;
        originalMoveDuration = 0f;
        hasOriginalCameraZoom = false;
        IsActive = false;
    }

    protected override void Activate(GameObject player)
    {
        GameManager manager = FindFirstObjectByType<GameManager>();
        if (manager == null)
        {
            Debug.LogWarning($"{nameof(TehTarikPowerup)} could not find {nameof(GameManager)} to change hop speed.", this);
            return;
        }

        if (activeRoutine == null || activeManager == null)
        {
            originalMoveDuration = manager.MoveDuration;
        }
        else
        {
            activeManager.StopCoroutine(activeRoutine);
        }

        activeManager = manager;
        IsActive = true;

        float multiplier = Mathf.Max(MinSpeedMultiplier, speedMultiplier);
        manager.SetMoveDuration(originalMoveDuration / multiplier);
        manager.PlayCameraShake(CameraShakeDuration, shakeMagnitude);
        StartSpeedLines(Camera.main, speedLinesPrefab);
        StartCameraZoom(manager, Camera.main);

        activeRoutine = manager.StartCoroutine(RestoreAfterDuration(manager, Mathf.Max(0f, duration), originalMoveDuration));
    }

    private static IEnumerator RestoreAfterDuration(GameManager manager, float seconds, float restoreMoveDuration)
    {
        if (seconds > 0f)
        {
            yield return new WaitForSeconds(seconds);
        }

        if (manager != null)
        {
            manager.SetMoveDuration(restoreMoveDuration);
        }

        StopSpeedLines(clearParticles: false);
        StartCameraZoomRestore(manager);

        if (activeManager == manager)
        {
            activeRoutine = null;
            activeManager = null;
            originalMoveDuration = 0f;
            IsActive = false;
        }
    }

    private static void StartSpeedLines(Camera camera, ParticleSystem prefab)
    {
        if (camera == null)
        {
            return;
        }

        StopSpeedLines(clearParticles: true);

        if (prefab != null)
        {
            activeSpeedLines = Instantiate(prefab, camera.transform);
            activeSpeedLinesObject = activeSpeedLines.gameObject;
            activeSpeedLinesObject.name = "TehTarik_SpeedLines";
            activeSpeedLines.transform.localPosition = Vector3.forward * SpeedLinesCameraDistance;
            activeSpeedLines.transform.localRotation = Quaternion.identity;
            activeSpeedLines.transform.localScale = Vector3.one;
            activeSpeedLines.Play(true);
            return;
        }

        activeSpeedLines = TehTarikSpeedLinesEffect.Create(camera, SpeedLinesCameraDistance);
        activeSpeedLinesObject = activeSpeedLines != null ? activeSpeedLines.gameObject : null;
    }

    private static void StopSpeedLines(bool clearParticles)
    {
        if (activeSpeedLinesObject != null &&
            activeSpeedLinesObject.TryGetComponent(out TehTarikSpeedLinesEffect speedLinesEffect))
        {
            speedLinesEffect.StopEmitting();
        }

        if (activeSpeedLines != null)
        {
            activeSpeedLines.Stop(true, clearParticles
                ? ParticleSystemStopBehavior.StopEmittingAndClear
                : ParticleSystemStopBehavior.StopEmitting);
        }

        if (activeSpeedLinesObject != null)
        {
            Destroy(activeSpeedLinesObject, clearParticles ? 0f : SpeedLinesDestroyDelay);
        }

        activeSpeedLines = null;
        activeSpeedLinesObject = null;
    }

    private static void StartCameraZoom(GameManager manager, Camera camera)
    {
        if (manager == null || camera == null)
        {
            return;
        }

        if (!hasOriginalCameraZoom || activeCamera != camera)
        {
            activeCamera = camera;
            originalCameraWasOrthographic = camera.orthographic;
            originalCameraZoom = GetCameraZoom(camera);
            hasOriginalCameraZoom = true;
        }

        StopActiveZoomRoutine();

        float currentZoom = GetCameraZoom(camera);
        float targetZoom = originalCameraZoom * CameraZoomMultiplier;
        activeZoomHost = manager;
        activeZoomRoutine = manager.StartCoroutine(LerpCameraZoom(camera, currentZoom, targetZoom, CameraZoomDuration, clearOriginalOnComplete: false));
    }

    private static void StartCameraZoomRestore(GameManager manager)
    {
        if (manager == null || activeCamera == null || !hasOriginalCameraZoom)
        {
            return;
        }

        StopActiveZoomRoutine();

        activeZoomHost = manager;
        activeZoomRoutine = manager.StartCoroutine(LerpCameraZoom(
            activeCamera,
            GetCameraZoom(activeCamera),
            originalCameraZoom,
            CameraZoomDuration,
            clearOriginalOnComplete: true));
    }

    private static IEnumerator LerpCameraZoom(Camera camera, float startZoom, float targetZoom, float seconds, bool clearOriginalOnComplete)
    {
        if (camera == null)
        {
            ClearZoomRoutineState(clearOriginalOnComplete);
            yield break;
        }

        if (seconds <= 0f)
        {
            SetCameraZoom(camera, targetZoom);
            ClearZoomRoutineState(clearOriginalOnComplete);
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < seconds && camera != null)
        {
            float t = Mathf.Clamp01(elapsed / seconds);
            SetCameraZoom(camera, Mathf.Lerp(startZoom, targetZoom, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (camera != null)
        {
            SetCameraZoom(camera, targetZoom);
        }

        ClearZoomRoutineState(clearOriginalOnComplete);
    }

    private static void StopActiveZoomRoutine()
    {
        if (activeZoomRoutine != null && activeZoomHost != null)
        {
            activeZoomHost.StopCoroutine(activeZoomRoutine);
        }

        activeZoomRoutine = null;
        activeZoomHost = null;
    }

    private static void RestoreCameraZoomImmediate()
    {
        if (activeCamera == null || !hasOriginalCameraZoom)
        {
            return;
        }

        if (originalCameraWasOrthographic)
        {
            activeCamera.orthographicSize = Mathf.Max(0.01f, originalCameraZoom);
            return;
        }

        activeCamera.fieldOfView = Mathf.Clamp(originalCameraZoom, 1f, 179f);
    }

    private static float GetCameraZoom(Camera camera)
    {
        return camera.orthographic ? camera.orthographicSize : camera.fieldOfView;
    }

    private static void SetCameraZoom(Camera camera, float zoom)
    {
        if (camera.orthographic)
        {
            camera.orthographicSize = Mathf.Max(0.01f, zoom);
            return;
        }

        camera.fieldOfView = Mathf.Clamp(zoom, 1f, 179f);
    }

    private static void ClearZoomRoutineState(bool clearOriginal)
    {
        activeZoomRoutine = null;
        activeZoomHost = null;

        if (!clearOriginal)
        {
            return;
        }

        activeCamera = null;
        originalCameraZoom = 0f;
        hasOriginalCameraZoom = false;
    }
}

internal sealed class TehTarikSpeedLinesEffect : MonoBehaviour
{
    private const int MaxParticles = 256;
    private const int LinesPerSecond = 140;
    private const float LineLifetime = 0.1f;
    private const float LineSpeed = 9f;
    private const float LineSpawnRadius = 0.05f;
    private const float LineThickness = 0.035f;
    private const float RendererVelocityScale = 0.08f;
    private const float RendererLengthScale = 2.6f;
    private static readonly Color LineColor = new(1f, 0.92f, 0.72f, 0.85f);

    private ParticleSystem particles;
    private Material lineMaterial;
    private float emissionAccumulator;

    public static ParticleSystem Create(Camera camera, float cameraDistance)
    {
        if (camera == null)
        {
            return null;
        }

        GameObject effectObject = new("TehTarik_SpeedLines");
        effectObject.transform.SetParent(camera.transform, false);
        effectObject.transform.localPosition = Vector3.forward * Mathf.Max(camera.nearClipPlane + 0.1f, cameraDistance);
        effectObject.transform.localRotation = Quaternion.identity;
        effectObject.transform.localScale = Vector3.one;

        ParticleSystem particleSystem = effectObject.AddComponent<ParticleSystem>();
        TehTarikSpeedLinesEffect effect = effectObject.AddComponent<TehTarikSpeedLinesEffect>();
        effect.Configure(particleSystem);
        return particleSystem;
    }

    private void Update()
    {
        if (!enabled || particles == null)
        {
            return;
        }

        emissionAccumulator += LinesPerSecond * Time.deltaTime;
        int emitCount = Mathf.FloorToInt(emissionAccumulator);
        if (emitCount <= 0)
        {
            return;
        }

        emissionAccumulator -= emitCount;
        for (int i = 0; i < emitCount; i++)
        {
            EmitLine();
        }
    }

    private void OnDestroy()
    {
        if (lineMaterial != null)
        {
            Destroy(lineMaterial);
            lineMaterial = null;
        }
    }

    private void Configure(ParticleSystem particleSystem)
    {
        particles = particleSystem;

        ParticleSystem.MainModule main = particles.main;
        main.duration = 1f;
        main.loop = true;
        main.playOnAwake = false;
        main.startLifetime = LineLifetime;
        main.startSpeed = 0f;
        main.startSize = LineThickness;
        main.startColor = LineColor;
        main.gravityModifier = 0f;
        main.maxParticles = MaxParticles;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Local;

        ParticleSystem.EmissionModule emission = particles.emission;
        emission.enabled = false;

        ParticleSystem.ShapeModule shape = particles.shape;
        shape.enabled = false;

        ParticleSystemRenderer particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Stretch;
        particleRenderer.velocityScale = RendererVelocityScale;
        particleRenderer.lengthScale = RendererLengthScale;
        particleRenderer.sortingOrder = short.MaxValue;

        lineMaterial = CreateLineMaterial();
        if (lineMaterial != null)
        {
            particleRenderer.material = lineMaterial;
        }

        particles.Play();
    }

    private void EmitLine()
    {
        float angle = Random.Range(0f, Mathf.PI * 2f);
        Vector3 direction = new(Mathf.Cos(angle), Mathf.Sin(angle), 0f);

        ParticleSystem.EmitParams emitParams = new()
        {
            position = direction * LineSpawnRadius,
            velocity = direction * LineSpeed,
            startLifetime = LineLifetime,
            startSize = LineThickness,
            startColor = LineColor
        };

        particles.Emit(emitParams, 1);
    }

    public void StopEmitting()
    {
        enabled = false;
    }

    private static Material CreateLineMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit") ??
                        Shader.Find("Particles/Standard Unlit") ??
                        Shader.Find("Sprites/Default");

        if (shader == null)
        {
            return null;
        }

        return new Material(shader)
        {
            name = "Teh Tarik Speed Lines",
            hideFlags = HideFlags.DontSave
        };
    }
}
