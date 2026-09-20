using UnityEngine;

/// <summary>One final-wave leader. Telegraph locks to a ground position, allowing the survivor to dodge.</summary>
[RequireComponent(typeof(HealthSystem))]
public sealed class FinalSiegeBoss : MonoBehaviour
{
    public static FinalSiegeBoss Active { get; private set; }
    public const float SlamRadius = 1.65f;
    public bool IsWindingUp { get; private set; }
    public Vector2 StrikePosition { get; private set; }
    public HealthSystem Health { get; private set; }
    private float _nextStrike, _impactAt, _flashUntil;
    private LineRenderer _ring;
    private Material _material;
    private Transform _ringRoot;
    private Tower _tower;

    private void Awake()
    {
        Health = GetComponent<HealthSystem>();
        Health.ConfigureFinalBoss();
        if (TryGetComponent<EnemyAttackBehavior>(out var movement)) movement.RegisterSiegeBoss(this);
    }
    private void OnEnable() { Active = this; }
    private void Start()
    {
        _tower = Tower.FindClosestLivingTower(transform.position);
        _nextStrike = Time.time + 3f;
        GameFlowManager.Instance?.ShowGameplayNotice("骸骨领主来袭  |  离开橙色预警圈，保护净化塔", 7f, true);
        _ringRoot = new GameObject("Boss ground telegraph").transform;
        _ring = _ringRoot.gameObject.AddComponent<LineRenderer>();
        _material = new Material(Shader.Find("Sprites/Default"));
        _ring.sharedMaterial = _material;
        _ring.useWorldSpace = true; _ring.loop = true; _ring.positionCount = 64;
        _ring.sortingLayerName = SortingLayerNames.Characters; _ring.sortingOrder = -1;
        _ring.enabled = false;
    }
    private void Update()
    {
        if (Health.CurrentHp <= 0 || GameFlowManager.Instance == null || !GameFlowManager.Instance.IsPlaying) return;
        if (!IsWindingUp && Time.time >= _nextStrike)
        {
            var player = MapController.Instance != null ? MapController.Instance.Player : null;
            Transform target = player != null && Vector2.Distance(player.transform.position, transform.position) < 6f &&
                ReachableGround.HasLineOfSight(transform.position, player.transform.position, player.transform)
                ? player.transform : _tower != null ? _tower.transform : null;
            if (target != null && Vector2.Distance(target.position, transform.position) < 7f &&
                ReachableGround.HasLineOfSight(transform.position, target.position, target)) BeginStrike(target.position);
        }
        if (IsWindingUp)
        {
            float charge = 1f - Mathf.Clamp01((_impactAt - Time.time) / 1.35f);
            DrawRing(Mathf.Lerp(.035f, .11f, charge), Color.Lerp(new Color(1f, .7f, .25f, .7f), new Color(1f, .3f, .15f, 1), charge));
            if (Time.time >= _impactAt)
            {
                IsWindingUp = false;
                _nextStrike = Time.time + 5f; _flashUntil = Time.time + .22f;
                Hit(MapController.Instance.PlayerHealth, 30);
                if (_tower != null) Hit(_tower.Health, 450);
                GameAudio.Play("Attack", .3f);
            }
        }
        else if (Time.time >= _flashUntil) _ring.enabled = false;
    }
    private void BeginStrike(Vector2 position)
    {
        StrikePosition = position; IsWindingUp = true; _impactAt = Time.time + 1.35f;
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI * 2 / 64;
            _ring.SetPosition(i, position + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * SlamRadius);
        }
    }
    private void DrawRing(float width, Color color)
    {
        _ring.enabled = true; _ring.startWidth = _ring.endWidth = width;
        _ring.startColor = _ring.endColor = color;
    }
    private void Hit(HealthSystem target, float damage)
    {
        if (target == null || Vector2.Distance(target.transform.position, StrikePosition) > SlamRadius ||
            !ReachableGround.HasLineOfSight(StrikePosition, target.transform.position, target.transform)) return;
        target.TakeDamage(damage, gameObject);
    }
    private void OnDisable()
    {
        if (Active == this) Active = null;
        IsWindingUp = false;
        if (_ring != null) _ring.enabled = false;
    }
    private void OnDestroy()
    {
        if (_ringRoot != null) Destroy(_ringRoot.gameObject);
        if (_material != null) Destroy(_material);
    }
}
