using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>Geographic navigation, not a second camera. Reads current ring bounds and real obstacles.</summary>
public sealed class ExplorationMapUI : MonoBehaviour
{
    public static ExplorationMapUI Instance { get; private set; }
    public static bool Expanded => Instance != null && Instance.IsExpanded;
    public bool IsExpanded { get; private set; }
    public readonly HashSet<ExplorationSupply> Discovered = new HashSet<ExplorationSupply>();
    public BuildingRegionVisibility[] Buildings { get; private set; }
    public MapZone[] Zones { get; private set; }
    private GameObject _root, _mini, _full;
    private NavigationGraphic _miniGraphic, _fullGraphic;
    private TextMeshProUGUI _miniTitle, _legend, _title, _closeLabel, _location, _regionHeading, _legendHeading, _hint, _footer;
    private readonly TextMeshProUGUI[] _regionLabels = new TextMeshProUGUI[3];
    private readonly TextMeshProUGUI[] _regionBadges = new TextMeshProUGUI[3];
    private readonly Image[] _regionCards = new Image[3];
    private float _nextRefresh;
    private TowerUpgradePanelUI _upgrade;
    private RectTransform _journeyStatus;
    private readonly List<MapTerrainGraphic> _terrain = new List<MapTerrainGraphic>();
    public bool MiniVisible => _mini != null && _mini.activeInHierarchy;
    public float OuterRadius
    {
        get { float max = 1; foreach (var z in Zones) { z.GetRingParams(out _, out _, out float r); max = Mathf.Max(max, r); } return max; }
    }

    private void Start()
    {
        Instance = this;
        Zones = FindObjectsOfType<MapZone>();
        Buildings = FindObjectsOfType<BuildingRegionVisibility>();
        _upgrade = FindObjectOfType<TowerUpgradePanelUI>();
        _journeyStatus = GameObject.Find("JourneyStatus")?.GetComponent<RectTransform>();
        Build();
    }
    private void LateUpdate()
    {
        var flow = GameFlowManager.Instance; var map = MapController.Instance;
        if (_root == null || flow == null || map == null || map.Player == null) return;
        if (IsExpanded && flow.CurrentState != GameState.Paused) IsExpanded = false;
        bool visible = flow.IsPlaying || IsExpanded;
        _root.SetActive(visible);
        if (!visible) return;
        if (Input.GetKeyDown(KeyCode.M) || Input.GetKeyDown(KeyCode.Tab)) Toggle();
        _mini.SetActive(!IsExpanded && (_upgrade == null || !_upgrade.IsVisible));
        _full.SetActive(IsExpanded);
        if (_mini.activeSelf) PlaceMiniMap();
        if (Time.unscaledTime < _nextRefresh) return;
        _nextRefresh = Time.unscaledTime + .1f;
        Discover(map.Player.transform.position);
        _miniGraphic.SetVerticesDirty(); _fullGraphic.SetVerticesDirty();
        foreach (var terrain in _terrain) { terrain.SetVerticesDirty(); terrain.SetMaterialDirty(); }
        RefreshLabels();
    }
    private void PlaceMiniMap()
    {
        var rect = (RectTransform)_mini.transform;
        rect.anchoredPosition = new Vector2(24,24);
        if(_journeyStatus == null || !_journeyStatus.gameObject.activeInHierarchy) return;
        Rect footer = ScreenBounds(_journeyStatus), mini = ScreenBounds(rect);
        if(mini.Overlaps(footer))
            rect.anchoredPosition += Vector2.up * ((footer.yMax-mini.yMin)/_root.GetComponent<Canvas>().scaleFactor+12);
    }
    public static Rect ScreenBounds(RectTransform rect)
    {
        var corners = new Vector3[4]; rect.GetWorldCorners(corners);
        return UnityEngine.Rect.MinMaxRect(corners[0].x,corners[0].y,corners[2].x,corners[2].y);
    }
    public void Discover(Vector2 position)
    {
        foreach (var supply in ExplorationSupply.Available)
            if (supply != null && MapController.Instance.IsMapUnlocked(supply.Region) &&
                Vector2.Distance(position, supply.transform.position) <= 8f) Discovered.Add(supply);
        Discovered.RemoveWhere(s => s == null);
    }
    public void Toggle()
    {
        var flow = GameFlowManager.Instance;
        if (IsExpanded) { Close(); return; }
        if (flow == null || !flow.IsPlaying) return;
        IsExpanded = true;
        flow.PauseGame();
        _mini.SetActive(false); _full.SetActive(true);
        RefreshLabels(); _fullGraphic.SetVerticesDirty();
    }
    public bool Close()
    {
        if (!IsExpanded) return false;
        IsExpanded = false;
        _full.SetActive(false);
        if (GameFlowManager.Instance != null && GameFlowManager.Instance.CurrentState == GameState.Paused)
            GameFlowManager.Instance.ResumeGame();
        return true;
    }
    private void RefreshLabels()
    {
        var map = MapController.Instance;
        int region = map.GetRegionAt(map.Player.transform.position);
        float distance = Vector2.Distance(map.Player.transform.position, map.GetMapCenter());
        _miniTitle.text = GameText.English ? $"MAP [M]  ·  {distance:0}m" : $"地图 [M]  ·  {distance:0}米";
        _title.text = GameText.English ? "FIELD MAP" : "三区地图";
        _closeLabel.text = GameText.English ? "Close  [ESC]" : "关闭  [ESC]";
        _location.text = GameText.English ? $"<size=20><color=#96ABA7>YOUR POSITION</color></size>\n<size=32>REGION {region}</size>\n<color=#CDB17B>Tower  ·  {distance:0}m</color>" :
            $"<size=20><color=#96ABA7>当前位置</color></size>\n<size=32>第 {region} 区</size>\n<color=#CDB17B>距塔  ·  {distance:0}米</color>";
        _regionHeading.text = GameText.English ? "REGIONS" : "区域状态";
        _legendHeading.text = GameText.English ? "MAP KEY" : "地图图例";
        for (int i = 1; i <= 3; i++)
        {
            bool open = map.IsMapUnlocked(i), clear = GameFlowManager.Instance.IsRegionCleared(i);
            string state = GameText.English ? (!open ? "Tower Lv." + i : clear ? "Cleared" : "Open") : (!open ? "塔 " + i + " 级解锁" : clear ? "已净化" : "已解锁");
            _regionLabels[i-1].text = $"<color={(open ? "#DDD3BD" : "#778582")}>{i:00}   {(GameText.English ? "REGION " + i : "第 " + i + " 区")}<pos=53%><size=21>{state}</size></color>";
            _regionCards[i-1].color = region == i ? new Color(.22f,.23f,.14f) : new Color(.075f,.085f,.062f);
            var z = System.Array.Find(Zones, zone => zone.MapLevel == i);
            if(z != null)
            {
                z.GetRingParams(out _,out float inner,out float outer);
                float s = Mathf.Min(_fullGraphic.rectTransform.rect.width,_fullGraphic.rectTransform.rect.height)*.465f / OuterRadius;
                float r = (inner + outer)*.5f*s;
                _regionBadges[i-1].rectTransform.anchoredPosition = new Vector2(-r*.6f,r*.8f);
                _regionBadges[i-1].color = open ? new Color(.76f,.71f,.59f) : new Color(.38f,.43f,.43f);
            }
        }
        _legend.text = GameText.English ?
            "<color=#FFFFFF>◆</color>   You\n<color=#E9BF70>▣</color>   Tower\n<color=#E9BF70>◆</color>   Clear reward\n<color=#7CDBCA>◆</color>   Supplies\n<color=#85BFFF>+</color>   Field repair\n<color=#9B9C9A>■</color>   Buildings" :
            "<color=#FFFFFF>◆</color>   玩家\n<color=#E9BF70>▣</color>   净化塔\n<color=#E9BF70>◆</color>   清区补给\n<color=#7CDBCA>◆</color>   已发现补给\n<color=#85BFFF>+</color>   修塔补给\n<color=#9B9C9A>■</color>   建筑";
        _hint.text = GameText.English ? "Explore to reveal supplies." : "探索附近建筑，发现补给。";
        _footer.text = GameText.English ? "PAUSED    ·    M / ESC to return" : "已暂停    ·    M / ESC 返回游戏";
    }
    private void Build()
    {
        _root = new GameObject("ExplorationMapCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        _root.transform.SetParent(transform, false);
        _root.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
        _root.GetComponent<Canvas>().overrideSorting = true;
        _root.GetComponent<Canvas>().sortingOrder = 500;
        var scaler = _root.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920,1080); scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
        RectTransform mini = Rect("MiniMap", _root.transform, Vector2.one, Vector2.one);
        mini.anchorMin = mini.anchorMax = Vector2.zero; mini.pivot = Vector2.zero;
        mini.sizeDelta = new Vector2(264,292); mini.anchoredPosition = new Vector2(24,24);
        _mini = mini.gameObject; Background(mini, new Color(.025f,.04f,.045f,.94f));
        Skin(mini);
        _miniGraphic = Graphic("MiniMapGeometry", mini, new Vector2(.12f,.10f), new Vector2(.88f,.78f), false);
        _miniTitle = Label("MapButtonLabel", mini, new Vector2(.12f,.80f), new Vector2(.88f,.91f), 20);
        _mini.GetComponent<Image>().raycastTarget = true;
        _mini.AddComponent<Button>().onClick.AddListener(Toggle);
        RectTransform full = Rect("ExpandedMap", _root.transform, Vector2.zero, Vector2.one); _full=full.gameObject;
        Background(full, new Color(.055f,.063f,.048f,1)).raycastTarget = true;
        Skin(full);
        _title=Label("MapTitle", full, new Vector2(.045f,.88f), new Vector2(.66f,.97f), 36);
        _title.alignment=TextAlignmentOptions.Left;
        var atlas = Rect("MapSurface",full,new Vector2(.04f,.11f),new Vector2(.67f,.875f));
        Background(atlas,new Color(.08f,.09f,.067f)); Skin(atlas);
        _fullGraphic=Graphic("FullMapGeometry",atlas,new Vector2(.025f,.025f),new Vector2(.975f,.975f),true);
        for(int i=0;i<3;i++)
        {
            _regionBadges[i]=Label("RegionBadge"+(i+1),_fullGraphic.transform,Vector2.one*.5f,Vector2.one*.5f,22);
            _regionBadges[i].rectTransform.sizeDelta=new Vector2(52,34); _regionBadges[i].text=(i+1).ToString("00");
        }
        var north=Label("North",atlas,new Vector2(.46f,.94f),new Vector2(.54f,.99f),18); north.text="N"; north.color=new Color(.55f,.65f,.62f);
        var side=Rect("MapDetails",full,new Vector2(.70f,.11f),new Vector2(.96f,.875f));
        Background(side,new Color(.055f,.063f,.048f)); Skin(side);
        side=Rect("MapDetailsContent",side,Vector2.zero,Vector2.one);
        side.offsetMin=new Vector2(39,36); side.offsetMax=new Vector2(-39,-36);
        _location=LeftLabel("MapPosition",side,new Vector2(.03f,.81f),new Vector2(.97f,1),24);
        _regionHeading=LeftLabel("RegionHeading",side,new Vector2(.03f,.735f),new Vector2(.97f,.79f),19);
        _regionHeading.color=new Color(.60f,.68f,.65f);
        for(int i=0;i<3;i++)
        {
            float y=.655f-i*.081f;
            var card=Rect("RegionCard"+(i+1),side,new Vector2(0,y),new Vector2(1,y+.069f));
            _regionCards[i]=Background(card,Color.black);
            _regionLabels[i]=LeftLabel("RegionState"+(i+1),card,new Vector2(.045f,.10f),new Vector2(.98f,.90f),23);
            _regionLabels[i].alignment=TextAlignmentOptions.MidlineLeft;
        }
        _legendHeading=LeftLabel("LegendHeading",side,new Vector2(.03f,.401f),new Vector2(.97f,.45f),19);
        _legendHeading.color=new Color(.60f,.68f,.65f);
        _legend=LeftLabel("MapLegend",side,new Vector2(.03f,.067f),new Vector2(.97f,.392f),24);
        _legend.lineSpacing=7;
        _hint=LeftLabel("MapHint",side,new Vector2(.03f,0),new Vector2(.98f,.048f),19); _hint.color=new Color(.60f,.68f,.65f);
        _footer=Label("MapFooter",full,new Vector2(.04f,.025f),new Vector2(.67f,.085f),21); _footer.color=new Color(.60f,.68f,.65f);
        var close=Rect("CloseMap",full,new Vector2(.835f,.90f),new Vector2(.96f,.955f));
        var closeImage=Background(close,new Color(.10f,.14f,.14f)); closeImage.raycastTarget=true;
        Frame(close,new Color(.48f,.40f,.27f));
        var closeButton=close.gameObject.AddComponent<Button>(); closeButton.targetGraphic=closeImage; closeButton.onClick.AddListener(()=>Close());
        _closeLabel=Label("Label",close,Vector2.zero,Vector2.one,23);
        _full.SetActive(false);
    }
    private static TextMeshProUGUI LeftLabel(string name,Transform parent,Vector2 min,Vector2 max,float size)
    { var label=Label(name,parent,min,max,size); label.alignment=TextAlignmentOptions.TopLeft; return label; }
    private static void Frame(RectTransform parent,Color color)
    {
        foreach(bool vertical in new[]{false,true}) foreach(float side in new[]{0f,1f})
        {
            var r=Rect("Border",parent,vertical?new Vector2(side,0):new Vector2(0,side),vertical?new Vector2(side,1):new Vector2(1,side));
            r.sizeDelta=vertical?new Vector2(2,0):new Vector2(0,2); Background(r,color);
        }
    }
    private NavigationGraphic Graphic(string name,Transform parent,Vector2 min,Vector2 max,bool full)
    {
        // Actual authored art, at its real world position and scale, below readable navigation markers.
        for (int level=3;level>=1;level--)
        foreach(var zone in Zones)
        {
            if(zone.MapLevel!=level) continue;
            var terrain=Rect(name+"Terrain"+level,parent,min,max).gameObject.AddComponent<MapTerrainGraphic>();
            terrain.Owner=this; terrain.Zone=zone; terrain.Full=full; terrain.raycastTarget=false;
            _terrain.Add(terrain);
        }
        var graphic=Rect(name,parent,min,max).gameObject.AddComponent<NavigationGraphic>();
        graphic.Owner=this; graphic.Full=full; graphic.raycastTarget=false; return graphic;
    }
    private void Skin(RectTransform panel)
    {
        var source=_journeyStatus!=null?_journeyStatus.GetComponent<Image>():null;
        if(source!=null && source.sprite!=null)
        {
            var frame=Rect("WoodFrame",panel,Vector2.zero,Vector2.one);
            var target=Background(frame,source.color);
            target.sprite=source.sprite; target.type=Image.Type.Sliced;
            target.pixelsPerUnitMultiplier=source.pixelsPerUnitMultiplier;
            // Match the existing HUD's dark inset; the frame sprite itself contains wood grain.
            var inset=Rect("PanelInset",panel,new Vector2(0,0),new Vector2(1,1));
            inset.offsetMin=new Vector2(31,30); inset.offsetMax=new Vector2(-31,-30);
            Background(inset,new Color(.055f,.063f,.048f,1));
        }
        else Frame(panel,new Color(.52f,.44f,.27f));
    }
    private static RectTransform Rect(string name,Transform parent,Vector2 min,Vector2 max)
    {
        var r=new GameObject(name,typeof(RectTransform)).GetComponent<RectTransform>(); r.SetParent(parent,false);
        r.anchorMin=min; r.anchorMax=max; r.offsetMin=r.offsetMax=Vector2.zero; return r;
    }
    private static Image Background(RectTransform rect,Color c) { var i=rect.gameObject.AddComponent<Image>(); i.color=c; i.raycastTarget=false; return i; }
    private static TextMeshProUGUI Label(string name,Transform parent,Vector2 min,Vector2 max,float size)
    {
        var t=Rect(name,parent,min,max).gameObject.AddComponent<TextMeshProUGUI>();
        t.font=Resources.Load<TMP_FontAsset>("Presentation/Chinese"); t.fontSize=size; t.color=new Color(.88f,.87f,.81f);
        t.alignment=TextAlignmentOptions.Center; t.raycastTarget=false; return t;
    }
    private void OnDestroy() { if(Instance==this) Instance=null; }
}

[RequireComponent(typeof(CanvasRenderer))]
public sealed class NavigationGraphic : MaskableGraphic
{
    public ExplorationMapUI Owner;
    public bool Full;
    private float _scale, _radius;
    private Vector2 _origin, _middle;
    private readonly Color _gold=new Color(.91f,.73f,.40f);
    protected override void OnPopulateMesh(VertexHelper v)
    {
        v.Clear(); var map=MapController.Instance;
        if(Owner==null || Owner.Zones==null || map==null || map.Player==null) return;
        _radius=Full?Owner.OuterRadius:map.GetMaxAllowedRadius();
        _scale=Mathf.Min(rectTransform.rect.width,rectTransform.rect.height)*.465f/_radius;
        _origin=map.GetMapCenter(); _middle=rectTransform.rect.center;
        for(int level=3;level>=1;level--)
        foreach(var z in Owner.Zones)
        {
            if(z.MapLevel!=level || (!Full && !z.IsUnlocked)) continue;
            z.GetRingParams(out _,out _,out float radius);
            // The terrain layer carries the art and locked-region tint.
            Ring(v,_middle,radius*_scale,z.IsUnlocked?new Color(.68f,.64f,.47f):new Color(.22f,.27f,.27f),Full?1:1.6f);
        }
        foreach(var building in Owner.Buildings)
        {
            if(building==null || !map.IsMapUnlocked(map.GetRegionAt(building.transform.position))) continue;
            var collider=building.GetComponent<PolygonCollider2D>(); if(collider==null) continue;
            var b=collider.bounds; Vector2 p=Point(b.center);
            float w=Mathf.Max(Full?8:3,b.size.x*_scale), h=Mathf.Max(Full?7:3,b.size.y*_scale);
            Color ink=new Color(.48f,.54f,.50f);
            Box(v,p,new Vector2(w,h),ink);
            if(Full) Triangle(v,p+new Vector2(-w*.6f,h*.5f),p+new Vector2(0,h*.5f+4),p+new Vector2(w*.6f,h*.5f),ink);
        }
        foreach(var supply in Owner.Discovered)
        {
            if(supply==null || !map.IsMapUnlocked(supply.Region)) continue;
            Vector2 p=Point(supply.transform.position);
            if(supply.IsTowerRepair || (Tower.FindClosestLivingTower(_origin)?.IsMaxLevel ?? false))
            { Box(v,p,new Vector2(3,10),new Color(.48f,.72f,1)); Box(v,p,new Vector2(10,3),new Color(.48f,.72f,1)); }
            else Diamond(v,p,5,new Color(.48f,.86f,.78f));
        }
        var cache=GameFlowManager.Instance?.ActiveSupplyCache;
        if(cache!=null) Diamond(v,Point(cache.transform.position),Full?7:5,_gold);
        Vector2 player=Point(map.Player.transform.position);
        Line(v,player,_middle,Full?1.4f:1,new Color(.82f,.72f,.44f,.4f));
        float towerSize=Full?14:12;
        Box(v,_middle,new Vector2(towerSize+4,towerSize+4),new Color(.05f,.07f,.07f));
        Box(v,_middle,new Vector2(towerSize,towerSize),_gold);
        Box(v,_middle,new Vector2(towerSize*.45f,towerSize*.45f),new Color(.09f,.1f,.085f));
        Disc(v,player,9,new Color(.05f,.07f,.07f));
        Diamond(v,player,7,Color.white);
    }
    private Vector2 Point(Vector2 world) => _middle+Vector2.ClampMagnitude(world-_origin,_radius)*_scale;
    private static void Triangle(VertexHelper v,Vector2 a,Vector2 b,Vector2 c,Color color)
    { int i=v.currentVertCount; v.AddVert(a,color,Vector2.zero); v.AddVert(b,color,Vector2.zero); v.AddVert(c,color,Vector2.zero); v.AddTriangle(i,i+1,i+2); }
    private static void Disc(VertexHelper v,Vector2 p,float r,Color c)
    { for(int i=0;i<96;i++) { float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96; Triangle(v,p,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,c); } }
    private static void Ring(VertexHelper v,Vector2 p,float r,Color c,float width)
    { for(int i=0;i<96;i++) {float a=i*Mathf.PI*2/96,b=(i+1)*Mathf.PI*2/96; Line(v,p+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*r,p+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*r,width,c); } }
    private static void Line(VertexHelper v,Vector2 a,Vector2 b,float width,Color c)
    { Vector2 d=b-a,n=new Vector2(-d.y,d.x).normalized*width*.5f; Triangle(v,a-n,a+n,b+n,c); Triangle(v,a-n,b+n,b-n,c); }
    private static void Box(VertexHelper v,Vector2 p,Vector2 size,Color c)
    {var h=size*.5f; Triangle(v,p-h,p+new Vector2(-h.x,h.y),p+h,c); Triangle(v,p-h,p+h,p+new Vector2(h.x,-h.y),c);}
    private static void Diamond(VertexHelper v,Vector2 p,float r,Color c)
    { Triangle(v,p+Vector2.left*r,p+Vector2.up*r,p+Vector2.right*r,c); Triangle(v,p+Vector2.left*r,p+Vector2.right*r,p+Vector2.down*r,c); }
}
