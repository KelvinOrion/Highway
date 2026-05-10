using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;

[InitializeOnLoad]
public static class BeritaMatiFontAssetBuilder
{
    private const string SourceFolder = "Assets/Resources/Fonts";
    private const string OutputFolder = "Assets/Resources/Fonts/TMP";
    private const int SamplingPointSize = 90;
    private const int Padding = 9;
    private const int AtlasWidth = 1024;
    private const int AtlasHeight = 1024;

    static BeritaMatiFontAssetBuilder()
    {
        EditorApplication.delayCall += () => EnsureFontAssets(false);
    }

    [MenuItem("Tools/Berita Mati/Rebuild TMP Font Assets")]
    public static void RebuildFontAssets()
    {
        EnsureFontAssets(true);
    }

    private static void EnsureFontAssets(bool force)
    {
        Directory.CreateDirectory(OutputFolder);
        Build("UnifrakturMaguntia-Book.ttf", "UnifrakturMaguntia.asset", force);
        Build("PlayfairDisplay-Variable.ttf", "PlayfairDisplay-Bold.asset", force);
        Build("PlayfairDisplay-Italic-Variable.ttf", "PlayfairDisplay-Italic.asset", force);
        Build("IMFeENrm28P.ttf", "IMFellEnglish.asset", force);
        Build("IMFeENit28P.ttf", "IMFellEnglish-Italic.asset", force);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void Build(string sourceFileName, string outputFileName, bool force)
    {
        string sourcePath = $"{SourceFolder}/{sourceFileName}";
        string outputPath = $"{OutputFolder}/{outputFileName}";

        if (!force && AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null)
        {
            return;
        }

        Font sourceFont = AssetDatabase.LoadAssetAtPath<Font>(sourcePath);
        if (sourceFont == null)
        {
            Debug.LogWarning($"Berita Mati font source missing: {sourcePath}");
            return;
        }

        if (AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(outputPath) != null)
        {
            AssetDatabase.DeleteAsset(outputPath);
        }

        TMP_FontAsset fontAsset = TMP_FontAsset.CreateFontAsset(
            sourceFont,
            SamplingPointSize,
            Padding,
            GlyphRenderMode.SDFAA,
            AtlasWidth,
            AtlasHeight,
            AtlasPopulationMode.Dynamic,
            true);

        fontAsset.name = Path.GetFileNameWithoutExtension(outputFileName);
        AssetDatabase.CreateAsset(fontAsset, outputPath);

        if (fontAsset.material != null)
        {
            fontAsset.material.name = $"{fontAsset.name} Material";
            AssetDatabase.AddObjectToAsset(fontAsset.material, fontAsset);
        }

        if (fontAsset.atlasTextures != null)
        {
            foreach (Texture2D atlasTexture in fontAsset.atlasTextures)
            {
                if (atlasTexture == null)
                {
                    continue;
                }

                atlasTexture.name = $"{fontAsset.name} Atlas";
                AssetDatabase.AddObjectToAsset(atlasTexture, fontAsset);
            }
        }

        EditorUtility.SetDirty(fontAsset);
    }
}
