/// <summary>
/// Project-wide 2D rendering layers. Their order is defined in ProjectSettings/TagManager.asset.
/// Keep per-object variations in sortingOrder instead of moving sprites on the Z axis.
/// </summary>
public static class SortingLayerNames
{
    public const string Background = "Background";
    public const string Ground = "Ground";
    public const string Environment = "Environment";
    public const string Characters = "Characters";
    public const string Foreground = "Foreground";
    public const string UI = "UI";
}
