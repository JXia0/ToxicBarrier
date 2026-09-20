using UnityEngine;
using UnityEngine.UI;

/// <summary>A feathered screen edge with a transparent centre; no texture or post processing required.</summary>
[DisallowMultipleComponent]
public sealed class ParasiteVignetteGraphic : MaskableGraphic
{
    private const int Columns = 48;
    private const int Rows = 32;

    protected override void Awake()
    {
        base.Awake();
        raycastTarget = false;
    }

    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = rectTransform.rect;
        float feather = Mathf.Max(1f, Mathf.Min(rect.width, rect.height) * 0.16f);
        for (int y = 0; y <= Rows; y++)
        {
            float v = (float)y / Rows;
            for (int x = 0; x <= Columns; x++)
            {
                float u = (float)x / Columns;
                float horizontal = Mathf.SmoothStep(1f, 0f,
                    Mathf.Min(u, 1f - u) * rect.width / feather);
                float vertical = Mathf.SmoothStep(1f, 0f,
                    Mathf.Min(v, 1f - v) * rect.height / feather);
                // Union of the two fades keeps the corners continuous without double opacity.
                float opacity = 1f - (1f - horizontal) * (1f - vertical);
                Color tint = Color.Lerp(new Color(0.7f, 0.065f, 0.12f),
                    new Color(0.38f, 0.015f, 0.055f), opacity);
                tint.a = opacity;
                mesh.AddVert(new Vector3(rect.xMin + u * rect.width, rect.yMin + v * rect.height),
                    tint, new Vector2(u, v));
                if (x == Columns || y == Rows) continue;
                int i = y * (Columns + 1) + x;
                mesh.AddTriangle(i, i + Columns + 1, i + 1);
                mesh.AddTriangle(i + 1, i + Columns + 1, i + Columns + 2);
            }
        }
    }
}
