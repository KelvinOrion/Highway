using System.IO;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(Readme))]
[InitializeOnLoad]
public class ReadmeEditor : Editor
{
    private const string ShowedReadmeSessionStateName = "ReadmeEditor.showedReadme";
    private const string ReadmeSourceDirectory = "Assets/TutorialInfo";
    private const float SectionSpacing = 16f;
    private const float MaxIconWidth = 128f;
    private const float IconWidthDivisor = 3f;
    private const float IconWidthPadding = 20f;
    private const int BodyFontSize = 14;
    private const int TitleFontSize = 26;
    private const int HeadingFontSize = 18;

    [SerializeField] private GUIStyle linkStyle;
    [SerializeField] private GUIStyle titleStyle;
    [SerializeField] private GUIStyle headingStyle;
    [SerializeField] private GUIStyle bodyStyle;
    [SerializeField] private GUIStyle buttonStyle;

    private bool initialized;

    private GUIStyle LinkStyle => linkStyle;
    private GUIStyle TitleStyle => titleStyle;
    private GUIStyle HeadingStyle => headingStyle;
    private GUIStyle BodyStyle => bodyStyle;
    private GUIStyle ButtonStyle => buttonStyle;

    static ReadmeEditor()
    {
        EditorApplication.delayCall += SelectReadmeAutomatically;
    }

    private static void RemoveTutorial()
    {
        if (!EditorUtility.DisplayDialog(
                "Remove Readme Assets",
                $"All contents under {ReadmeSourceDirectory} will be removed, are you sure you want to proceed?",
                "Proceed",
                "Cancel"))
        {
            return;
        }

        if (Directory.Exists(ReadmeSourceDirectory))
        {
            FileUtil.DeleteFileOrDirectory(ReadmeSourceDirectory);
            FileUtil.DeleteFileOrDirectory(ReadmeSourceDirectory + ".meta");
        }

        Readme readmeAsset = SelectReadme();
        if (readmeAsset != null)
        {
            string assetPath = AssetDatabase.GetAssetPath(readmeAsset);
            FileUtil.DeleteFileOrDirectory(assetPath + ".meta");
            FileUtil.DeleteFileOrDirectory(assetPath);
        }

        AssetDatabase.Refresh();
    }

    // Opens the tutorial readme once per editor session.
    private static void SelectReadmeAutomatically()
    {
        if (SessionState.GetBool(ShowedReadmeSessionStateName, false))
        {
            return;
        }

        Readme readme = SelectReadme();
        SessionState.SetBool(ShowedReadmeSessionStateName, true);

        if (readme != null && !readme.LoadedLayout)
        {
            readme.MarkLayoutLoaded();
            EditorUtility.SetDirty(readme);
        }
    }

    private static Readme SelectReadme()
    {
        string[] ids = AssetDatabase.FindAssets("Readme t:Readme");
        if (ids.Length != 1)
        {
            return null;
        }

        Object readmeObject = AssetDatabase.LoadMainAssetAtPath(AssetDatabase.GUIDToAssetPath(ids[0]));
        Selection.objects = new[] { readmeObject };

        return (Readme)readmeObject;
    }

    protected override void OnHeaderGUI()
    {
        Readme readme = (Readme)target;
        Init();

        float iconWidth = Mathf.Min(EditorGUIUtility.currentViewWidth / IconWidthDivisor - IconWidthPadding, MaxIconWidth);

        GUILayout.BeginHorizontal("In BigTitle");
        if (readme.Icon != null)
        {
            GUILayout.Space(SectionSpacing);
            GUILayout.Label(readme.Icon, GUILayout.Width(iconWidth), GUILayout.Height(iconWidth));
        }

        GUILayout.Space(SectionSpacing);
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();
        GUILayout.Label(readme.Title, TitleStyle);
        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    public override void OnInspectorGUI()
    {
        Readme readme = (Readme)target;
        Init();

        foreach (Readme.Section section in readme.Sections)
        {
            if (!string.IsNullOrEmpty(section.Heading))
            {
                GUILayout.Label(section.Heading, HeadingStyle);
            }

            if (!string.IsNullOrEmpty(section.Text))
            {
                GUILayout.Label(section.Text, BodyStyle);
            }

            if (!string.IsNullOrEmpty(section.LinkText) && LinkLabel(new GUIContent(section.LinkText)))
            {
                Application.OpenURL(section.Url);
            }

            GUILayout.Space(SectionSpacing);
        }

        if (GUILayout.Button("Remove Readme Assets", ButtonStyle))
        {
            RemoveTutorial();
        }
    }

    private void Init()
    {
        if (initialized)
        {
            return;
        }

        bodyStyle = new GUIStyle(EditorStyles.label)
        {
            wordWrap = true,
            fontSize = BodyFontSize,
            richText = true
        };

        titleStyle = new GUIStyle(bodyStyle)
        {
            fontSize = TitleFontSize
        };

        headingStyle = new GUIStyle(bodyStyle)
        {
            fontStyle = FontStyle.Bold,
            fontSize = HeadingFontSize
        };

        linkStyle = new GUIStyle(bodyStyle)
        {
            wordWrap = false,
            stretchWidth = false
        };

        // Match selection color for both light and dark editor skins.
        linkStyle.normal.textColor = new Color(0x00 / 255f, 0x78 / 255f, 0xDA / 255f, 1f);

        buttonStyle = new GUIStyle(EditorStyles.miniButton)
        {
            fontStyle = FontStyle.Bold
        };

        initialized = true;
    }

    private bool LinkLabel(GUIContent label, params GUILayoutOption[] options)
    {
        Rect position = GUILayoutUtility.GetRect(label, LinkStyle, options);

        Handles.BeginGUI();
        Handles.color = LinkStyle.normal.textColor;
        Handles.DrawLine(new Vector3(position.xMin, position.yMax), new Vector3(position.xMax, position.yMax));
        Handles.color = Color.white;
        Handles.EndGUI();

        EditorGUIUtility.AddCursorRect(position, MouseCursor.Link);

        return GUI.Button(position, label, LinkStyle);
    }
}
