using UnityEngine;

/// <summary>
/// Map-owned bridge: selects the correct LevelWavesConfig for the current MapId
/// and broadcasts changes when MapController switches maps.
///
/// The enemy spawn system can depend on this selector without MapZone coupling.
/// </summary>
public class MapWaveConfigSelector : MonoBehaviour
{
    [Header("Configs")]
    [SerializeField] private LevelWavesConfig[] levelConfigs;

    [Header("Debug")]
    [SerializeField] private bool logSelection;

    public LevelWavesConfig CurrentConfig { get; private set; }
    public int CurrentMapId { get; private set; } = 1;

    /// <summary> Fired when the selected config changes. Args: mapId, selectedConfig (may be null). </summary>
    public event System.Action<int, LevelWavesConfig> OnWaveConfigChanged;

    private void OnEnable()
    {
        if (MapController.Instance != null)
            MapController.Instance.OnMapIdChanged += HandleMapIdChanged;
    }

    private void Start()
    {
        int mapId = MapController.Instance != null ? MapController.Instance.CurrentMapId : 1;
        SelectForMap(mapId, forceNotify: true);
    }

    private void OnDisable()
    {
        if (MapController.Instance != null)
            MapController.Instance.OnMapIdChanged -= HandleMapIdChanged;
    }

    private void HandleMapIdChanged(int previousMapId, int newMapId)
    {
        SelectForMap(newMapId, forceNotify: false);
    }

    private void SelectForMap(int mapId, bool forceNotify)
    {
        CurrentMapId = Mathf.Max(1, mapId);
        LevelWavesConfig selected = FindConfigForMap(CurrentMapId);

        bool changed = selected != CurrentConfig;
        CurrentConfig = selected;

        if (logSelection)
            Debug.Log($"[MapWaveConfigSelector] MapId={CurrentMapId}, Waves={(CurrentConfig != null ? CurrentConfig.name : "NULL")}", this);

        if (forceNotify || changed)
            OnWaveConfigChanged?.Invoke(CurrentMapId, CurrentConfig);
    }

    private LevelWavesConfig FindConfigForMap(int mapId)
    {
        if (levelConfigs == null) return null;

        for (int i = 0; i < levelConfigs.Length; i++)
        {
            LevelWavesConfig cfg = levelConfigs[i];
            if (cfg != null && cfg.mapId == mapId)
                return cfg;
        }
        return null;
    }
}

