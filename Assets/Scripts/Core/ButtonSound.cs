using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public sealed class ButtonSound : MonoBehaviour, IPointerEnterHandler
{
    private Button _button;
    private void Awake() { _button = GetComponent<Button>(); _button.onClick.AddListener(Click); }
    private void Click() { GameAudio.Play("Click"); }
    public void OnPointerEnter(PointerEventData data) { if (_button.interactable) GameAudio.Play("Hover", .15f); }
}
