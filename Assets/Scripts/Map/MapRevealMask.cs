using UnityEngine;

/// <summary>
/// Attach to Map 1 (or the object that defines the "revealed" area). Creates or uses a Sprite Mask
/// so that Map 2/3 fog (VisibleOutsideMask) only shows outside this circle — i.e. only a small part of locked maps is visible near the edge.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class MapRevealMask : MonoBehaviour
{
    [Tooltip("Leave empty to use this object's SpriteRenderer sprite and transform.")]
    [SerializeField] private SpriteMask spriteMaskSource;

    private SpriteMask _mask;

    private void Start()
    {
        EnsureMask();
    }

    private void EnsureMask()
    {
        if (_mask != null) return;

        if (spriteMaskSource != null)
        {
            _mask = spriteMaskSource;
            return;
        }

        Transform maskT = transform.Find("RevealMask");
        if (maskT != null)
        {
            _mask = maskT.GetComponent<SpriteMask>();
            if (_mask != null) return;
        }

        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        if (sr == null || sr.sprite == null) return;

        GameObject maskGo = new GameObject("RevealMask");
        maskGo.transform.SetParent(transform, false);
        maskGo.transform.localPosition = Vector3.zero;
        maskGo.transform.localScale = Vector3.one;
        maskGo.transform.localRotation = Quaternion.identity;

        _mask = maskGo.AddComponent<SpriteMask>();
        _mask.sprite = sr.sprite;
        _mask.alphaCutoff = 0.5f;
    }
}
