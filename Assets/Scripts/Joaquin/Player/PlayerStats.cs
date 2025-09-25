using UnityEngine;

/// <summary>
/// Función que maneja las estadísticas del jugador.
/// </summary>
[CreateAssetMenu(fileName = "NewEntityStats", menuName = "Stats/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Vida")]
    public float maxHealth = 100f;

    [Header("Movimiento")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;

    [Header("Ataque General")]
    public float attackDamage = 1.0f;
    public float attackSpeed = 1.0f;

    [Header("Ataque melee")]
    public int meleeAttackDamage = 10;
    public float meleeSpeed = 1f;
    public float meleeRadius = 0.8f;

    [Header("Ataque a distancia")]
    public int shieldAttackDamage = 10;
    public float shieldSpeed = 20f;
    public float shieldMaxDistance = 30f;
    public int shieldMaxRebounds = 2;
    public float shieldReboundRadius = 15f;

    [Header("Habilidades")]
    public float HealthDrainAmount = 2f;
}