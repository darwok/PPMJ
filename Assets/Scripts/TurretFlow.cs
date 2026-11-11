using UnityEngine;

public class TurretFlow : MonoBehaviour
{
    [Header("Tipo de Torreta de Flujo (Lanzallamas o Manguera de Agua)")]
    [SerializeField] private EnumCardType turretType = EnumCardType.Turret4_Lanzallamas;
    [Tooltip("Activa esta casilla en el prefab de la Torreta 5 de Agua para que empuje enemigos sin hacer daño")]
    [SerializeField] private bool isWaterHoseKnockbackOnly = false;
    [SerializeField] private float baseWaterKnockbackForce = 1.2f;

    [Header("Atributos Base")]
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireRate = 0.08f;
    [SerializeField] private float damage = 3f;
    [SerializeField] private float projectileLifetime = 0.5f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    private float effectiveRange;
    private float effectiveFireRate;
    private float effectiveDamage;
    private float effectiveKnockback;
    private float nextFireTime;

    private void Start()
    {
        if (name.ToLower().Contains("water") || name.ToLower().Contains("agua") || turretType == EnumCardType.Turret5_WaterHose)
        {
            turretType = EnumCardType.Turret5_WaterHose;
            isWaterHoseKnockbackOnly = true;
        }

        // Aplicar los Perks guardados al instanciarse esta nueva torreta
        float speedMult = RunProgressionData.GetTotalAttackSpeedMultiplierForNewTurret(turretType);
        effectiveFireRate = fireRate / Mathf.Max(0.1f, speedMult);
        effectiveRange = range + RunProgressionData.GetTotalExtraRangeForNewTurret(turretType);

        if (isWaterHoseKnockbackOnly)
        {
            effectiveDamage = 0f;
            effectiveKnockback = RunProgressionData.GetWaterKnockbackForce(baseWaterKnockbackForce);
        }
        else
        {
            effectiveDamage = RunProgressionData.CalculateFinalDamageForNewTurret(turretType, damage);
            effectiveKnockback = 0f;
        }
    }

    private void Update()
    {
        EnemyFSM targetEnemy = FindNearestEnemy();
        if (targetEnemy == null)
            return;

        Vector3 enemyCenter = targetEnemy.GetTargetCenter();

        Vector3 flatDirection = enemyCenter - transform.position;
        flatDirection.y = 0f;

        if (flatDirection.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(flatDirection);

        float overchargeMultiplier = GameManager.Instance != null
            ? GameManager.Instance.GetTurretFireRateMultiplier()
            : 1f;

        float currentInterval = effectiveFireRate / Mathf.Max(0.1f, overchargeMultiplier);

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + currentInterval;
            FireAt(enemyCenter);
        }
    }

    private EnemyFSM FindNearestEnemy()
    {
        EnemyFSM nearest = null;
        float minDistance = effectiveRange;

        foreach (EnemyFSM enemy in FindObjectsByType<EnemyFSM>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(
                transform.position,
                enemy.GetTargetCenter()
            );

            if (distance <= minDistance)
            {
                minDistance = distance;
                nearest = enemy;
            }
        }

        return nearest;
    }

    private void FireAt(Vector3 enemyCenter)
    {
        Transform spawn = firePoint != null ? firePoint : transform;
        Vector3 spawnPosition = spawn.position + Vector3.up * 0.35f;

        Vector3 aimDirection = enemyCenter - spawnPosition;

        Quaternion shotRotation = aimDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(aimDirection)
            : spawn.rotation;

        GameObject prefabToUse = bulletPrefab != null
            ? bulletPrefab
            : Bullet.GetOrCreateFallbackPrefab();

        Bullet.SpawnFromPool(
            prefabToUse,
            spawnPosition,
            shotRotation,
            customDamage: effectiveDamage,
            isAreaCluster: false,
            explosionRadius: 0f,
            customLifetime: projectileLifetime,
            customKnockbackForce: effectiveKnockback
        );
    }
}