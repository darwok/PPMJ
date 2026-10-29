using UnityEngine;

public enum CardType
{
    Turret1_Area,     // Daño en área (corto/medio alcance)
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
        float effectiveInterval = fireRate / overchargeMult;

        if (Time.time >= nextFireTime)
        {
            nextFireTime = Time.time + effectiveInterval;
            FireAt(targetEnemy, enemyCenter);
        }
    }

    private EnemyFSM FindNearestEnemy()
    {
        EnemyFSM nearest = null;
        float minDist = range;

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
        if (isAreaDamage)
        {
            // Torreta 1: Daño en área alrededor del objetivo
            Collider[] hits = Physics.OverlapSphere(enemyCenter, areaRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                EnemyFSM e = hit.GetComponentInParent<EnemyFSM>();
                if (e != null) e.TakeDamage(damage);
            }
            return;
        }

        if (bulletPrefab != null)
        {
            Transform spawn = firePoint != null ? firePoint : transform;
            Vector3 spawnPos = spawn.position + Vector3.up * 0.3f;

            // Apuntar en 3D (incluyendo la inclinación en Y hacia abajo) directo al centro del enemigo
            Vector3 aimDir3D = (enemyCenter - spawnPos).normalized;
            Quaternion shotRot = aimDir3D.sqrMagnitude > 0.001f ? Quaternion.LookRotation(aimDir3D) : spawn.rotation;

            Bullet.SpawnFromPool(bulletPrefab, spawnPos, shotRot, damage);
        }
        else
        {
            // Si no se asignó prefab de bala en el Inspector, aplicar daño directo al enemigo
            target.TakeDamage(damage);
        }
    }
}