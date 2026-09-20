using UnityEngine;
using UnityEngine.AI;

/// <summary>Shared ground/cover rules for pickups and combat. Trigger volumes never act as walls.</summary>
public static class ReachableGround
{
    private static readonly Collider2D[] Overlaps = new Collider2D[16];
    private static readonly RaycastHit2D[] Hits = new RaycastHit2D[32];
    private static readonly NavMeshPath Path = new NavMeshPath();
    public static readonly int ObstacleMask = LayerMask.GetMask("Obstacle");

    public static bool IsOpen(Vector2 point, float clearance = .3f)
    {
        int count = Physics2D.OverlapCircleNonAlloc(point, clearance, Overlaps, ObstacleMask);
        if (count == Overlaps.Length) return false;
        for (int i = 0; i < count; i++) if (!Overlaps[i].isTrigger) return false;
        return true;
    }

    public static bool TryFind(Vector2 desired, float searchRadius, out Vector2 position)
    {
        position = desired;
        if (!NavMesh.SamplePosition(desired, out NavMeshHit hit, searchRadius, NavMesh.AllAreas) || !IsOpen(hit.position)) return false;
        Tower tower = Tower.FindClosestLivingTower(hit.position);
        Vector3 origin = tower != null ? tower.transform.position + Vector3.down * 1.8f :
            MapController.Instance != null ? (Vector3)MapController.Instance.GetMapCenter() : Vector3.zero;
        if (!NavMesh.SamplePosition(origin, out NavMeshHit start, 3f, NavMesh.AllAreas) ||
            !NavMesh.CalculatePath(start.position, hit.position, NavMesh.AllAreas, Path) ||
            Path.status != NavMeshPathStatus.PathComplete) return false;
        position = hit.position;
        return true;
    }

    public static bool HasLineOfSight(Vector2 from, Vector2 to, Transform target = null)
    {
        Vector2 delta = to - from;
        int count = Physics2D.RaycastNonAlloc(from, delta.normalized, Hits, delta.magnitude, ObstacleMask);
        if (count == Hits.Length) return false;
        for (int i = 0; i < count; i++)
        {
            Collider2D wall = Hits[i].collider;
            if (wall.isTrigger || (target != null && (wall.transform == target || wall.transform.IsChildOf(target)))) continue;
            return false;
        }
        return true;
    }
}
