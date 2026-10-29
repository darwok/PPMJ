using UnityEngine;

public enum CardType
{
    Turret1_Area,     // Daño en área (dispara proyectil grande que explota en sub-balas)
    Turret2_Rapid,    // Cadencia rápida, daño medio-bajo
    Turret3_Heavy,    // Daño alto, cadencia lenta
    NewWagon          // Acoplar un nuevo vagón al convoy
}

public class Turret : MonoBehaviour
{
    [Header("Atributos de la Torreta")]
    public CardType turretType = CardType.Turret2_Rapid;
    [SerializeField] private float range = 18f;
    [SerializeField] private float fireRate = 0.4f;
    [SerializeField] private float damage = 15f;
    [SerializeField] private bool isAreaDamage = false;
    [SerializeField] private float areaRadius = 4.5f;
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;

    private float nextFireTime;

    void Start()
    {
        // Si la torreta es Turret1_Area, asegurar que isAreaDamage esté activo automáticamente
        if (turretType == CardType.Turret1_Area)
        {
            isAreaDamage = true;
        }
    }

    void Update()
    {
        EnemyFSM targetEnemy = FindNearestEnemy();
        if (targetEnemy == null) return;

        Vector3 enemyCenter = targetEnemy.GetTargetCenter();

        // Rotar el cuerpo de la torreta horizontalmente hacia el enemigo
        Vector3 flatDir = enemyCenter - transform.position;
        flatDir.y = 0f;
        if (flatDir.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(flatDir);
        }

        float overchargeMult = GameManager.Instance != null ? GameManager.Instance.GetTurretFireRateMultiplier() : 1f;
        float effectiveInterval = fireRate / Mathf.Max(0.1f, overchargeMult);

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + effectiveInterval;
            FireAt(targetEnemy, enemyCenter);
        }
    }

    private EnemyFSM FindNearestEnemy()
    {
        EnemyFSM nearest = null;
        float bonusRange = GameManager.Instance != null ? GameManager.Instance.globalAttackRangeBonus : 0f;
        float minDist = range + bonusRange;

        foreach (EnemyFSM enemy in FindObjectsByType<EnemyFSM>(FindObjectsSortMode.None))
        {
            float dist = Vector3.Distance(transform.position, enemy.transform.position);
            if (dist <= minDist)
            {
                minDist = dist;
                nearest = enemy;
            }
        }
        return nearest;
    }

    private void FireAt(EnemyFSM target, Vector3 enemyCenter)
    {
        Transform spawn = firePoint != null ? firePoint : transform;
        Vector3 spawnPos = spawn.position + Vector3.up * 0.35f;

        // Apuntar en 3D (incluyendo la inclinación en Y hacia abajo) directo al centro del enemigo
        Vector3 aimDir3D = (enemyCenter - spawnPos).normalized;
        Quaternion shotRot = aimDir3D.sqrMagnitude > 0.001f ? Quaternion.LookRotation(aimDir3D) : spawn.rotation;

        GameObject prefabToUse = bulletPrefab != null ? bulletPrefab : Bullet.GetOrCreateFallbackPrefab();

        if (isAreaDamage || turretType == CardType.Turret1_Area)
        {
            // Torreta 1 (Área): Dispara una bala pesada (1.8x más grande) que al impactar detona en un anillo de 8 balas en el radio de efecto
            Bullet.SpawnFromPool(
                prefabToUse,
                spawnPos,
                shotRot,
                customDamage: damage,
                isAreaCluster: true,
                explosionRadius: areaRadius,
                scaleMultiplier: 1.85f
            );
        }
        else
        {
            // Torreta 2 (Rápida) y Torreta 3 (Pesada)
            float scaleMult = (turretType == CardType.Turret3_Heavy) ? 1.4f : 1.0f;
            Bullet.SpawnFromPool(
                prefabToUse,
                spawnPos,
                shotRot,
                customDamage: damage,
                isAreaCluster: false,
                explosionRadius: 0f,
                scaleMultiplier: scaleMult
            );
        }
    }
}