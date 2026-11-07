using UnityEngine;

public class TurretFlow : MonoBehaviour
{
    [Header("Atributos del lanzallamas")]
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireRate = 0.08f;
    [SerializeField] private float damage = 3f;
    [SerializeField] private float projectileLifetime = 0.5f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    private float nextFireTime;

    private void Start()
    {
        if (bulletPrefab == null)
        {
            Debug.LogError(
                "TurretFlow necesita un prefab de proyectil de llamas.",
                this
            );

            enabled = false;
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

        float effectiveInterval = fireRate / Mathf.Max(0.1f, overchargeMultiplier);

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + effectiveInterval;
            FireAt(enemyCenter);
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

    private void FireAt(Vector3 enemyCenter)
    {
        Transform spawn = firePoint != null ? firePoint : transform;
        Vector3 spawnPosition = spawn.position + Vector3.up * 0.35f;

        Vector3 aimDirection = enemyCenter - spawnPosition;

        Quaternion shotRotation = aimDirection.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(aimDirection)
            : spawn.rotation;

        Bullet.SpawnFromPool(
            bulletPrefab,
            spawnPosition,
            shotRotation,
            customDamage: damage,
            isAreaCluster: false,
            explosionRadius: 0f,
            customLifetime: projectileLifetime
        );
    }
}

