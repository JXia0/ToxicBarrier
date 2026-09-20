using UnityEngine;

/// <summary>Carry one finite repair parcel home, keeping it if the tower is full.</summary>
public sealed class TowerRepairCargo : MonoBehaviour
{
    public bool IsCarrying { get; private set; }
    private HealthSystem _playerHealth;
    private void Awake() { _playerHealth = GetComponent<HealthSystem>(); }
    public bool TryLoad()
    {
        if (IsCarrying || _playerHealth == null || _playerHealth.CurrentHp <= 0 ||
            GameFlowManager.Instance == null || !GameFlowManager.Instance.IsPlaying) return false;
        IsCarrying = true;
        return true;
    }
    private void Update()
    {
        if (!IsCarrying || _playerHealth == null || _playerHealth.CurrentHp <= 0 ||
            GameFlowManager.Instance == null || !GameFlowManager.Instance.IsPlaying) return;
        Tower tower = Tower.FindClosestLivingTower(transform.position);
        if (tower == null || tower.Health.CurrentHp >= tower.Health.MaxHp ||
            Vector2.Distance(transform.position, tower.transform.position) > 2f ||
            !ReachableGround.HasLineOfSight(transform.position, tower.transform.position, tower.transform)) return;
        IsCarrying = false;
        tower.Health.Heal(tower.Health.MaxHp * .15f);
        GameAudio.Play("Supply", .5f);
        GameFlowManager.Instance.ShowGameplayNotice("修塔补给已送达  |  净化塔恢复 15% 生命", 6);
    }
}
