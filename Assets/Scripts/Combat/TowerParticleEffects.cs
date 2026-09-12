using System.Collections.Generic;
using UnityEngine;

/// <summary>Level-specific, pooled tower particles. Source prefabs remain untouched.</summary>
[DisallowMultipleComponent]
public sealed class TowerParticleEffects : MonoBehaviour
{
    [Header("Upgrade: both effects play beneath the tower")]
    [SerializeField] private GameObject[] upgradePrefabs = new GameObject[2];
    [SerializeField, Min(.01f)] private float upgradeScale = .5f;
    [Tooltip("World-space offset from the bottom of the tower sprite.")]
    [SerializeField] private Vector3 footOffset = new Vector3(0, .1f, 0);
    [Header("Attack: levels 1 / 2 / 3 (level 0 uses level 1)")]
    [SerializeField] private GameObject[] attackPrefabs = new GameObject[3];
    [SerializeField, Min(.01f)] private float attackScale = .35f;
    [Tooltip("Emission point along the tower sprite height, from its bottom (0) to its top (1).")]
    [SerializeField, Range(0, 1)] private float muzzleHeight = .78f;
    [SerializeField] private Vector3 effectRotation = Vector3.zero;
    [SerializeField, Range(1, 16)] private int attackPoolLimit = 8;

    private sealed class Effect
    {
        public GameObject root;
        public GameObject prefab;
        public ParticleSystem[] particles;
        public float stopAt, hideAt;
        public bool stopped;
    }
    private readonly List<Effect> _attacks = new List<Effect>();
    private readonly List<Effect> _upgrades = new List<Effect>();
    private Transform _root;
    public int ActiveAttackCount => _attacks.FindAll(e => e.root.activeSelf).Count;
    public int ActiveUpgradeCount => _upgrades.FindAll(e => e.root.activeSelf).Count;
    public string LastAttackPrefab { get; private set; }
    public int AttackPlayCount { get; private set; }

    public bool PlayAttack(Vector3 position, int level)
    {
        if (Time.timeScale <= 0 || !isActiveAndEnabled || attackPrefabs.Length == 0) return false;
        GameObject prefab = attackPrefabs[Mathf.Clamp(level - 1, 0, attackPrefabs.Length - 1)];
        if (prefab == null) return false;
        Effect effect = _attacks.Find(e => e.prefab == prefab && !e.root.activeSelf);
        if (effect == null)
        {
            if (_attacks.Count >= attackPoolLimit)
            {
                effect = _attacks[0];
                foreach (Effect candidate in _attacks) if (candidate.hideAt < effect.hideAt) effect = candidate;
                // Levels change only twice per run; retire a mismatched slot when necessary.
                if (effect.prefab != prefab)
                {
                    _attacks.Remove(effect);
                    Destroy(effect.root);
                    effect = null;
                }
            }
            if (effect == null) { effect = Create(prefab, 20); _attacks.Add(effect); }
        }
        SpriteRenderer towerSprite = GetComponent<SpriteRenderer>();
        Vector3 muzzle = transform.position;
        if (towerSprite != null)
        {
            muzzle.x = towerSprite.bounds.center.x;
            muzzle.y = towerSprite.bounds.min.y + towerSprite.bounds.size.y * muzzleHeight;
        }
        Play(effect, muzzle, attackScale, .4f, 1.3f);
        Vector3 direction = position - muzzle;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        effect.root.transform.rotation = Quaternion.Euler(0, 0, angle) * Quaternion.Euler(effectRotation);
        LastAttackPrefab = prefab.name;
        AttackPlayCount++;
        return true;
    }

    public void PlayUpgrade()
    {
        if (!isActiveAndEnabled || Time.timeScale <= 0) return;
        SpriteRenderer towerSprite = GetComponent<SpriteRenderer>();
        Vector3 foot = transform.position;
        if (towerSprite != null) foot.y = towerSprite.bounds.min.y;
        foreach (GameObject prefab in upgradePrefabs)
        {
            if (prefab == null) continue;
            Effect effect = _upgrades.Find(e => e.prefab == prefab);
            if (effect == null)
            {
                effect = Create(prefab, towerSprite != null ? towerSprite.sortingOrder - 1 : 5);
                foreach (ParticleSystem particle in effect.particles)
                    particle.GetComponent<ParticleSystemRenderer>().sortingLayerID = towerSprite != null ? towerSprite.sortingLayerID : SortingLayer.NameToID(SortingLayerNames.Environment);
                _upgrades.Add(effect);
            }
            Play(effect, foot + footOffset, upgradeScale, .6f, 2f);
        }
    }

    private Effect Create(GameObject prefab, int order)
    {
        if (_root == null) _root = new GameObject("Tower Particle Pool").transform;
        GameObject instance = Instantiate(prefab, _root);
        instance.SetActive(false);
        ParticleSystem[] particles = instance.GetComponentsInChildren<ParticleSystem>(true);
        foreach (ParticleSystem particle in particles)
        {
            particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particle.main;
            main.playOnAwake = false;
            main.useUnscaledTime = false;
            main.stopAction = ParticleSystemStopAction.None;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var renderer = particle.GetComponent<ParticleSystemRenderer>();
            if (renderer != null) { renderer.sortingLayerName = SortingLayerNames.Characters; renderer.sortingOrder = order; }
        }
        return new Effect { root = instance, prefab = prefab, particles = particles };
    }

    private void Play(Effect effect, Vector3 position, float scale, float emissionTime, float lifetime)
    {
        foreach (ParticleSystem particle in effect.particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        effect.root.transform.SetPositionAndRotation(position, Quaternion.Euler(effectRotation));
        effect.root.transform.localScale = Vector3.one * scale;
        effect.root.SetActive(true);
        foreach (ParticleSystem particle in effect.particles) particle.Play(false);
        effect.stopAt = Time.time + emissionTime;
        effect.hideAt = Time.time + lifetime;
        effect.stopped = false;
    }

    private void Update() { Tick(_attacks); Tick(_upgrades); }
    private static void Tick(List<Effect> effects)
    {
        foreach (Effect effect in effects)
        {
            if (!effect.root.activeSelf) continue;
            if (!effect.stopped && Time.time >= effect.stopAt)
            {
                foreach (ParticleSystem particle in effect.particles) particle.Stop(false, ParticleSystemStopBehavior.StopEmitting);
                effect.stopped = true;
            }
            if (Time.time >= effect.hideAt) effect.root.SetActive(false);
        }
    }
    private void OnDisable()
    {
        foreach (Effect effect in _attacks) if (effect.root != null) effect.root.SetActive(false);
        foreach (Effect effect in _upgrades) if (effect.root != null) effect.root.SetActive(false);
    }
    private void OnDestroy() { if (_root != null) Destroy(_root.gameObject); }
}
