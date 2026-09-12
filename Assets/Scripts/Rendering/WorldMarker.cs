using UnityEngine;

/// <summary>Small world-space rings keep the player and mission pickup readable on dense backgrounds.</summary>
public sealed class WorldMarker : MonoBehaviour
{
    private Material _material;
    public static WorldMarker Add(Transform parent, float radius, Color color, int order)
    {
        var go = new GameObject("Ground marker");
        // World coordinates avoid inheriting the character's facing flip or zero Z scale.
        var marker = go.AddComponent<WorldMarker>(); marker._target = parent;
        var line = go.AddComponent<LineRenderer>();
        marker._material = new Material(Shader.Find("Sprites/Default")); line.sharedMaterial = marker._material;
        line.useWorldSpace = false; line.loop = true; line.positionCount = 48; line.widthMultiplier = .045f;
        line.startColor = line.endColor = color; line.sortingOrder = order;
        line.sortingLayerName = SortingLayerNames.Characters;
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2 / 48;
            line.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius * .5f, 0));
        }
        return marker;
    }
    private Transform _target;
    private void LateUpdate()
    {
        if (_target == null) { Destroy(gameObject); return; }
        transform.position = _target.position;
    }
    private void OnDestroy() { if (_material != null) Destroy(_material); }
}
