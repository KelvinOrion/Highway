#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

public static class GenerateSpeedLineTexture
{
    private const string OutputDirectory = "Assets/Textures";
    private const string OutputPath = OutputDirectory + "/SpeedLines.png";
    private const int TextureSize = 512;
    private const int LineCount = 24;
    private const float AngleStep = 360f / LineCount;

    private static readonly Vector2 Center = new Vector2(256f, 256f);
    private static readonly Color32 OpaqueWhite = new Color32(255, 255, 255, 255);

    [MenuItem("Tools/Generate Speed Lines Texture")]
    public static void Generate()
    {
        Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        texture.SetPixels32(new Color32[TextureSize * TextureSize]);

        for (int i = 0; i < LineCount; i++)
        {
            float mainAngle = i * AngleStep;
            DrawRadialLine(texture, mainAngle, 220f, 256f, 6f, 1f);

            float secondaryAngle = mainAngle + (AngleStep * 0.5f);
            DrawRadialLine(texture, secondaryAngle, 230f, 253f, 2f, 2f);
        }

        texture.Apply();

        Directory.CreateDirectory(OutputDirectory);
        File.WriteAllBytes(OutputPath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.Refresh();
        Debug.Log($"Generated speed lines texture at {OutputPath}");
    }

    private static void DrawRadialLine(
        Texture2D texture,
        float angleDegrees,
        float startRadius,
        float endRadius,
        float startWidth,
        float endWidth)
    {
        float radians = angleDegrees * Mathf.Deg2Rad;
        Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
        Vector2 perpendicular = new Vector2(-direction.y, direction.x);

        Vector2 start = Center + (direction * startRadius);
        Vector2 end = Center + (direction * endRadius);
        Vector2 startHalfWidth = perpendicular * (startWidth * 0.5f);
        Vector2 endHalfWidth = perpendicular * (endWidth * 0.5f);

        Vector2 a = start + startHalfWidth;
        Vector2 b = start - startHalfWidth;
        Vector2 c = end - endHalfWidth;
        Vector2 d = end + endHalfWidth;

        FillQuad(texture, a, b, c, d);
    }

    private static void FillQuad(Texture2D texture, Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        int minX = Mathf.Clamp(Mathf.FloorToInt(Min(a.x, b.x, c.x, d.x)), 0, TextureSize - 1);
        int maxX = Mathf.Clamp(Mathf.CeilToInt(Max(a.x, b.x, c.x, d.x)), 0, TextureSize - 1);
        int minY = Mathf.Clamp(Mathf.FloorToInt(Min(a.y, b.y, c.y, d.y)), 0, TextureSize - 1);
        int maxY = Mathf.Clamp(Mathf.CeilToInt(Max(a.y, b.y, c.y, d.y)), 0, TextureSize - 1);

        for (int y = minY; y <= maxY; y++)
        {
            for (int x = minX; x <= maxX; x++)
            {
                Vector2 point = new Vector2(x + 0.5f, y + 0.5f);
                if (IsPointInTriangle(point, a, b, c) || IsPointInTriangle(point, a, c, d))
                {
                    texture.SetPixel(x, y, OpaqueWhite);
                }
            }
        }
    }

    private static bool IsPointInTriangle(Vector2 point, Vector2 a, Vector2 b, Vector2 c)
    {
        float first = Sign(point, a, b);
        float second = Sign(point, b, c);
        float third = Sign(point, c, a);

        bool hasNegative = first < 0f || second < 0f || third < 0f;
        bool hasPositive = first > 0f || second > 0f || third > 0f;

        return !(hasNegative && hasPositive);
    }

    private static float Sign(Vector2 point, Vector2 a, Vector2 b)
    {
        return ((point.x - b.x) * (a.y - b.y)) - ((a.x - b.x) * (point.y - b.y));
    }

    private static float Min(float a, float b, float c, float d)
    {
        return Mathf.Min(Mathf.Min(a, b), Mathf.Min(c, d));
    }

    private static float Max(float a, float b, float c, float d)
    {
        return Mathf.Max(Mathf.Max(a, b), Mathf.Max(c, d));
    }
}
#endif
