using System.Collections.Generic;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;

/// <summary>The navigation mesh is extruded from the actual 2D collision paths, never a second hand-sized box.</summary>
public static class BuildingNavigationGeometry
{
    public static void Sync(GameObject building, PolygonCollider2D footprint)
    {
        Transform existing = building.transform.Find("NavigationObstacle");
        GameObject proxy = existing != null ? existing.gameObject : new GameObject("NavigationObstacle");
        proxy.transform.SetParent(building.transform, false);
        proxy.transform.localPosition = Vector3.zero;
        proxy.transform.localRotation = Quaternion.identity;
        proxy.transform.localScale = Vector3.one;
        proxy.layer = building.layer;
        foreach (BoxCollider box in proxy.GetComponents<BoxCollider>()) Object.DestroyImmediate(box);
        var vertices = new List<Vector3>();
        var triangles = new List<int>();
        for (int path = 0; path < footprint.pathCount; path++)
        {
            // Each authored footprint path is convex; multiple paths preserve open courtyards.
            Vector2[] points = footprint.GetPath(path);
            int start = vertices.Count;
            for (int side = 0; side < 2; side++)
                foreach (Vector2 point in points)
                    vertices.Add(new Vector3(point.x + footprint.offset.x, point.y + footprint.offset.y, side == 0 ? -.05f : 1f));
            int n = points.Length;
            for (int i = 1; i < n - 1; i++)
            {
                triangles.AddRange(new[] { start, start + i + 1, start + i,
                    start + n, start + n + i, start + n + i + 1 });
            }
            for (int i = 0; i < n; i++)
            {
                int next = (i + 1) % n;
                triangles.AddRange(new[] { start + i, start + next, start + n + i,
                    start + next, start + n + next, start + n + i });
            }
        }
        // Instances can have different offsets/paths despite sharing a sprite. Never overwrite another instance's bake source.
        string identity = GlobalObjectId.GetGlobalObjectIdSlow(building).ToString();
        Mesh mesh = new Mesh { name = building.GetComponent<SpriteRenderer>().sprite.name + "_" + Hash128.Compute(identity) + "_Footprint" };
        mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
        const string folder = "Assets/Navigation/Footprints";
        if (!Directory.Exists(folder)) { Directory.CreateDirectory(folder); AssetDatabase.Refresh(); }
        string pathName = folder + "/" + mesh.name + ".asset";
        Mesh saved = AssetDatabase.LoadAssetAtPath<Mesh>(pathName);
        if (saved != null) { EditorUtility.CopySerialized(mesh, saved); Object.DestroyImmediate(mesh); mesh = saved; EditorUtility.SetDirty(saved); }
        else AssetDatabase.CreateAsset(mesh, pathName);
        var collider = proxy.GetComponent<MeshCollider>();
        if (collider == null) collider = proxy.AddComponent<MeshCollider>();
        collider.sharedMesh = null; collider.sharedMesh = mesh;
        var modifier = proxy.GetComponent<NavMeshModifier>();
        if (modifier == null) modifier = proxy.AddComponent<NavMeshModifier>();
        modifier.overrideArea = true; modifier.area = 1;
    }
}
