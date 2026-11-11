using UnityEngine;

public class Turret : MonoBehaviour
{
    [SerializeField] private EnumCardType turretType = EnumCardType.Turret2_Rapid;

    [Header("Atributos Base de la Torreta")]
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireRate = 0.4f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private bool isAreaDamage;
    [SerializeField] private float areaRadius = 4.5f;
    [Tooltip("Fuerza de empuje en metros (usada cuando esta torreta es Turret5_WaterHose)")]
    [SerializeField] private float baseWaterKnockbackForce = 3.5f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    // Estadísticas efectivas tras aplicar los Perks guardados al momento de colocar esta nueva torreta
    private float effectiveRange;
    private float effectiveFireRate;
    private float effectiveDamage;
    private float effectiveKnockback;
    private float nextFireTime;

    private void Start()
    {
        if (turretType == EnumCardType.Turret1_Area)
            isAreaDamage = true;

        // Aplicar los Perks acumulados únicamente a las nuevas torretas al ser instanciadas
        float speedMult = RunProgressionData.GetTotalAttackSpeedMultiplierForNewTurret(turretType);
        effectiveFireRate = fireRate / Mathf.Max(0.1f, speedMult);
        effectiveRange = range + RunProgressionData.GetTotalExtraRangeForNewTurret(turretType);

        if (turretType == EnumCardType.Turret5_WaterHose)
        {
            // La torreta de agua no hace daño, sino que empuja a los enemigos hacia atrás
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
            FireAt(targetEnemy, enemyCenter);
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

    private void FireAt(EnemyFSM target, Vector3 enemyCenter)
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

        bool areaShot = isAreaDamage || turretType == EnumCardType.Turret1_Area;

        float scaleMultiplier = areaShot
            ? 1.85f
            : turretType == EnumCardType.Turret3_Heavy ? 1.4f : 1f;

        Bullet.SpawnFromPool(
            prefabToUse,
            spawnPosition,
            shotRotation,
            customDamage: effectiveDamage,
            isAreaCluster: areaShot,
            explosionRadius: areaShot ? areaRadius : 0f,
            scaleMultiplier: scaleMultiplier,
            homingTarget: target,
            customKnockbackForce: effectiveKnockback
        );
    }
}