using UnityEngine;

[DisallowMultipleComponent]
public class MalaysianRoadTextureController : MonoBehaviour
{
    private const float MinTextureRepeat = 0.01f;
    private const float MinRoadLengthRepeats = 1f;

    public enum RoadType
    {
        SingleNoLine,
        SingleDoubleLine,
        MultiLane,
        MultiLaneWithEmergencyShoulder
    }

    public enum RoadMode
    {
        SingleNoLine,
        SingleDoubleLine,
        MultiLane,
        MultiLaneWithEmergencyShoulder
    }

    [Header("Renderer")]
    [SerializeField] private Renderer[] roadRenderers;

    [Header("Materials")]
    [SerializeField] private Material singleNoLineMaterial;
    [SerializeField] private Material singleDoubleLineMaterial;
    [SerializeField] private Material multiLaneMaterial;
    [SerializeField] private Material multiLaneEmergencyMaterial;

    [Header("Tiling")]
    [SerializeField] private Vector2 singleLaneTiling = Vector2.one;
    [SerializeField] private Vector2 multiLaneTiling = Vector2.one;
    [SerializeField] private float worldUnitsPerTextureRepeat = 6f;

    [Header("Scrolling")]
    [SerializeField] private bool scrollRoad = false;
    [SerializeField] private float scrollSpeed = 1.5f;

    [Header("Runtime")]
    [SerializeField] private RoadType roadType = RoadType.MultiLaneWithEmergencyShoulder;

    private MaterialPropertyBlock block;
    private Vector2 offset;

    private static readonly int MainTexST = Shader.PropertyToID("_MainTex_ST");
    private static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");

    public RoadType CurrentRoadType => roadType;

    private void Awake()
    {
        ResolveRenderers();

        block = new MaterialPropertyBlock();
        ApplyRoadType();
    }

    private void OnValidate()
    {
        scrollSpeed = Mathf.Max(0f, scrollSpeed);
        worldUnitsPerTextureRepeat = Mathf.Max(MinTextureRepeat, worldUnitsPerTextureRepeat);

        singleLaneTiling.x = Mathf.Max(MinTextureRepeat, singleLaneTiling.x);
        singleLaneTiling.y = Mathf.Max(MinTextureRepeat, singleLaneTiling.y);
        multiLaneTiling.x = Mathf.Max(MinTextureRepeat, multiLaneTiling.x);
        multiLaneTiling.y = Mathf.Max(MinTextureRepeat, multiLaneTiling.y);
    }

    private void Update()
    {
        if (!scrollRoad)
            return;

        offset.y -= scrollSpeed * Time.deltaTime;
        ApplyTextureProperties();
    }

    public void SetRoadType(RoadType type)
    {
        roadType = type;
        ApplyRoadType();
    }

    public void SetRoadMode(RoadType mode)
    {
        SetRoadType(mode);
    }

    public void SetRoadMode(RoadMode mode)
    {
        SetRoadType((RoadType)mode);
    }

    private void ApplyRoadType()
    {
        offset = Vector2.zero;
        ApplyMaterial();
        ApplyTextureProperties();
    }

    private void ApplyMaterial()
    {
        Material material = GetMaterialForType();
        if (material == null)
        {
            Debug.LogWarning($"{nameof(MalaysianRoadTextureController)} has no material assigned for {roadType}.", this);
            return;
        }

        ResolveRenderers();

        for (int i = 0; i < roadRenderers.Length; i++)
        {
            if (roadRenderers[i] != null)
            {
                roadRenderers[i].sharedMaterial = material;
            }
        }
    }

    private void ApplyTextureProperties()
    {
        block ??= new MaterialPropertyBlock();

        Vector2 tiling = IsSingleLane() ? singleLaneTiling : multiLaneTiling;
        ResolveRenderers();

        for (int i = 0; i < roadRenderers.Length; i++)
        {
            Renderer targetRenderer = roadRenderers[i];
            if (targetRenderer == null)
            {
                continue;
            }

            targetRenderer.GetPropertyBlock(block);

            Vector3 boundsSize = targetRenderer.bounds.size;
            float roadLengthRepeats = Mathf.Max(MinRoadLengthRepeats, Mathf.Max(boundsSize.x, boundsSize.z) / worldUnitsPerTextureRepeat);
            Vector4 st = new(tiling.x, tiling.y * roadLengthRepeats, offset.x, offset.y);
            block.SetVector(MainTexST, st);
            block.SetVector(BaseMapST, st);

            targetRenderer.SetPropertyBlock(block);
        }
    }

    private Material GetMaterialForType()
    {
        return roadType switch
        {
            RoadType.SingleNoLine => singleNoLineMaterial,
            RoadType.SingleDoubleLine => singleDoubleLineMaterial,
            RoadType.MultiLane => multiLaneMaterial,
            RoadType.MultiLaneWithEmergencyShoulder => multiLaneEmergencyMaterial,
            _ => multiLaneMaterial
        };
    }

    private bool IsSingleLane()
    {
        return roadType == RoadType.SingleNoLine ||
               roadType == RoadType.SingleDoubleLine;
    }

    private void ResolveRenderers()
    {
        if (roadRenderers != null && roadRenderers.Length > 0)
        {
            return;
        }

        roadRenderers = GetComponentsInChildren<MeshRenderer>();
    }
}
