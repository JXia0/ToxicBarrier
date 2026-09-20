using UnityEngine;

/// <summary>A restrained silhouette rim in toxic/outer terrain, following the existing animated art.</summary>
[DefaultExecutionOrder(500)]
public sealed class SurvivorReadability : MonoBehaviour
{
    private SpriteRenderer _source, _rim;
    private Material _material;
    private void Awake()
    {
        _source = GetComponent<SpriteRenderer>();
        Shader shader = Resources.Load<Shader>("Presentation/SurvivorRim");
        if (_source == null || shader == null) { enabled = false; return; }
        _material = new Material(shader);
        var go = new GameObject("Survivor silhouette rim");
        go.transform.SetParent(transform, false);
        go.transform.localScale = new Vector3(1.045f, 1.025f, 1);
        _rim = go.AddComponent<SpriteRenderer>(); _rim.sharedMaterial = _material;
    }
    private void LateUpdate()
    {
        if (_rim == null) return;
        _rim.sprite = _source.sprite; _rim.flipX = _source.flipX; _rim.flipY = _source.flipY;
        _rim.sortingLayerID = _source.sortingLayerID; _rim.sortingOrder = _source.sortingOrder - 1;
        bool outer = MapController.Instance != null && MapController.Instance.GetRegionAt(transform.position) > 1;
        _rim.enabled = _source.enabled && outer;
        _rim.color = new Color(.65f, .78f, .85f, .42f);
    }
    private void OnDestroy() { if (_material != null) Destroy(_material); }
}
