using System;
using UnityEngine;

public class Readme : ScriptableObject
{
    [SerializeField] private Texture2D icon;
    [SerializeField] private string title;
    [SerializeField] private Section[] sections;
    [SerializeField] private bool loadedLayout;

    public Texture2D Icon => icon;
    public string Title => title;
    public Section[] Sections => sections;
    public bool LoadedLayout => loadedLayout;

    public void MarkLayoutLoaded()
    {
        loadedLayout = true;
    }

    [Serializable]
    public class Section
    {
        [SerializeField] private string heading;
        [SerializeField] private string text;
        [SerializeField] private string linkText;
        [SerializeField] private string url;

        public string Heading => heading;
        public string Text => text;
        public string LinkText => linkText;
        public string Url => url;
    }
}
