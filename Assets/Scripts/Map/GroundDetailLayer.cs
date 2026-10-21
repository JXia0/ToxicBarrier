using UnityEngine;

/// <summary>Fallback edge reconstruction for low-resolution terrain; HD art uses the normal sprite material.</summary>
[RequireComponent(typeof(SpriteRenderer))]
public sealed class GroundDetailLayer : MonoBehaviour
{
    private Material _material, _original;
    private SpriteRenderer _renderer;
    public bool HasDetail => _material != null;
    public bool UsesHighResolution => _renderer != null && _renderer.sprite != null && _renderer.sprite.texture.width >= 4096;
    private void Start()
    {
        var zone = GetComponent<MapZone>();
        if (zone == null || zone.MapLevel < 2) return;
        _renderer = GetComponent<SpriteRenderer>();
        if (UsesHighResolution) return;
        var shader = Resources.Load<Shader>("TerrainDetail/GroundDetail");
        if (shader == null || !shader.isSupported) return;
        _original = _renderer.sharedMaterial;
        _material = new Material(shader);
        _material.SetFloat("_Strength", 1f);
        _renderer.sharedMaterial = _material;
    }
    private void OnDestroy()
    {
        if (_renderer != null && _material != null) _renderer.sharedMaterial = _original;
        if (_material != null) Destroy(_material);
    }
}
