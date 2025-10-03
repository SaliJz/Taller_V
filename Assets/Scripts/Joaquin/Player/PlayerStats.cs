using UnityEngine;

/// <summary>
/// Función que maneja las estadísticas del jugador.
/// </summary>
[CreateAssetMenu(fileName = "NewEntityStats", menuName = "Stats/Player Stats")]
public class PlayerStats : ScriptableObject
{
    [Header("Vida")]
    public float maxHealth = 100f;
    public float currentHealth = 100f;

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
    public float lifestealOnKillAmount = 5f;
    public bool isShieldBlockUpgradeActive = false;
    public float healthPerRoomRegenBase = 2f;

    [Header("Generales")]
    public float shopPriceReductionBase = 0f;
    // --- VARIABLES DE STATS PENDIENTES DE CONEXIÓN ---
    /*
    public float essenceCostReductionBase = 0f;
    public float meleeStunChanceBase = 0f;
    public float rangedSlowStunChanceBase = 0f;
    public float criticalChanceBase = 0f;
    public float luckStackBase = 0f;
    public float fireDashEffectBase = 0f;
    public float residualDashEffectBase = 0f;
    public float stunnedOnHitChanceBase = 0f;
    public bool shieldCatchRequiredBase = false;
    public float sameAttackDamageReductionBase = 0f;
    public float missChanceBase = 0f;
    public float shieldDropChanceBase = 0f;
    public float berserkerEffectBase = 0f;
    public float dashRangeMultiplierBase = 1f;
    */
}