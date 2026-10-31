using UnityEngine;

[RequireComponent(typeof(EnemyAttack))]
public class MidBoss : MonoBehaviour
{
    public const float DamageMultiplier = 1.8f;
    EnemyAttack attack;
    bool switchPending;
    void Awake() => attack = GetComponent<EnemyAttack>();
    void OnEnable() => attack.AttackFired += OnFired;
    void OnDisable() => attack.AttackFired -= OnFired;
    void OnFired(EnemyAttack source) => switchPending = true;
    void LateUpdate()
    {
        if (!switchPending) return;
        switchPending = false;
        attack.SetKind(attack.Kind == EnemyAttackKind.Melee ? EnemyAttackKind.Ranged : EnemyAttackKind.Melee);
    }
}
