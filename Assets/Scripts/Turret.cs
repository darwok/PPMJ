using UnityEngine;

public class Turret : MonoBehaviour
{
    [SerializeField] private EnumCardType turretType = EnumCardType.Turret2_Rapid;

    [Header("Atributos de la Torreta")]
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireRate = 0.4f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private bool isAreaDamage;
    [SerializeField] private float areaRadius = 4.5f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    private float nextFireTime;

    private void Start()
    {
        if (turretType == EnumCardType.Turret1_Area)
            isAreaDamage = true;
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

        float effectiveInterval = fireRate / Mathf.Max(0.1f, overchargeMultiplier);

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + effectiveInterval;
            FireAt(targetEnemy, enemyCenter);
        }
    }

    private EnemyFSM FindNearestEnemy()
    {
        EnemyFSM nearest = null;

        float bonusRange = GameManager.Instance != null
            ? GameManager.Instance.globalAttackRangeBonus
            : 0f;

        float minDistance = range + bonusRange;

        foreach (EnemyFSM enemy in FindObjectsByType<EnemyFSM>(FindObjectsSortMode.None))
        {
            float distance = Vector3.Distance(
                transform.position,
                enemy.transform.position
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
            customDamage: damage,
            isAreaCluster: areaShot,
            explosionRadius: areaShot ? areaRadius : 0f,
            scaleMultiplier: scaleMultiplier,
            homingTarget: target
        );
    }
}