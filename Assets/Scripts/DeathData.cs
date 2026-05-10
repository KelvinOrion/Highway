using UnityEngine;
using UnityEngine.Serialization;

[CreateAssetMenu(fileName = "DeathData", menuName = "Highway/Death Data")]
public sealed class DeathData : ScriptableObject
{
    [SerializeField] private string causeName = "Myvi";

    [TextArea(1, 2)]
    [SerializeField] private string headline = "Mangsa Kena Langgar Myvi\nDi Lorong Sempit";

    [FormerlySerializedAs("headlineEN")]
    [FormerlySerializedAs("headlineEnglish")]
    [TextArea(1, 2)]
    [SerializeField] private string headlineEn = "Victim struck by Myvi in a narrow lane";

    [TextArea(1, 2)]
    [SerializeField] private string subhead = "\"Semua orang tahu Myvi bawak laju. Tapi dia tetap tak elak.\"";

    [FormerlySerializedAs("subheadEN")]
    [FormerlySerializedAs("subheadEnglish")]
    [TextArea(1, 2)]
    [SerializeField] private string subheadEn = "\"Everyone knows Myvis drive fast. He still didn't dodge.\"";

    [TextArea(1, 2)]
    [SerializeField] private string flavourText = "Allahyarham dikenali gemar makan nasi lemak pagi.\nSemoga rohnya tenang di jalan yang lebih selamat.";

    public string CauseName => causeName;
    public string Headline => headline;
    public string HeadlineEn => headlineEn;
    public string Subhead => subhead;
    public string SubheadEn => subheadEn;
    public string FlavourText => flavourText;

    public static DeathData CreateRuntime(string causeName, string headline, string headlineEn, string subhead, string subheadEn, string flavourText)
    {
        DeathData data = CreateInstance<DeathData>();
        data.causeName = causeName;
        data.headline = headline;
        data.headlineEn = headlineEn;
        data.subhead = subhead;
        data.subheadEn = subheadEn;
        data.flavourText = flavourText;
        return data;
    }
}
