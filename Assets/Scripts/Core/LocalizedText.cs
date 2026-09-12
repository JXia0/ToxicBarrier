using TMPro;
using UnityEngine;

/// <summary>Only static scene labels use this binding; gameplay values translate at their source.</summary>
[RequireComponent(typeof(TextMeshProUGUI))]
public sealed class LocalizedText : MonoBehaviour
{
    [TextArea] public string chinese;
    private void OnEnable() { GameText.Changed += Refresh; Refresh(); }
    private void OnDisable() { GameText.Changed -= Refresh; }
    public void Refresh() { GetComponent<TextMeshProUGUI>().text = GameText.Translate(chinese); }
}
