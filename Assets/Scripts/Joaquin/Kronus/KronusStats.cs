using UnityEngine;

[CreateAssetMenu(fileName = "NewEntityStats", menuName = "Stats/Kronus Stats")]
public class KronusStats : ScriptableObject
{
    [Header("Salud")]
    public float health = 15f;

    [Header("Movimiento")]
    public float moveSpeed = 3.5f;
    public float dashSpeedMultiplier = 2.5f;
    public float dashDuration = 0.4f;

    [Header("Combate")]
    public float attackCycleCooldown = 5f;
    public float attackDamagePercentage = 0.18f;
    public float attackRadius = 2f;
}