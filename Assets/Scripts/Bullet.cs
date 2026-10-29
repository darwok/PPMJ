using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Ajustes del Proyectil")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float hitRadius = 0.45f;

    // Configuración de disparo en área / fragmentación (para Torreta 1)
    private bool isAreaClusterBullet = false;
    private float areaExplosionRadius = 0f;
    private int shrapnelCount = 8;
    private Vector3 defaultLocalScale;
    private bool scaleRecorded = false;

    private float deactivateTime;
    private GameObject sourcePrefab;

    // Sistema de Object Pooling integrado
    private static readonly Dictionary<GameObject, Queue<Bullet>> pools = new Dictionary<GameObject, Queue<Bullet>>();
    private static Transform poolContainer;
    private static GameObject fallbackBulletPrefab;

    public static GameObject GetOrCreateFallbackPrefab()
    {
        if (fallbackBulletPrefab == null)
        {
            fallbackBulletPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackBulletPrefab.name = "DefaultPooledBullet_Prefab";
            fallbackBulletPrefab.transform.localScale = new Vector3(0.35f, 0.35f, 0.35f);
            Collider c = fallbackBulletPrefab.GetComponent<Collider>();
            if (c != null) c.isTrigger = true;
            fallbackBulletPrefab.AddComponent<Bullet>();
            fallbackBulletPrefab.SetActive(false);
        }
        return fallbackBulletPrefab;
    }

    public static Bullet SpawnFromPool(
        GameObject prefab,
        Vector3 position,
        Quaternion rotation,
        float customDamage = -1f,
        bool isAreaCluster = false,
        float explosionRadius = 4.5f,
        float scaleMultiplier = 1f,
        float customLifetime = -1f)
    {
        if (prefab == null)
        {
            prefab = GetOrCreateFallbackPrefab();
        }

        if (poolContainer == null)
        {
            GameObject containerObj = new GameObject("_BulletPool_Container");
            poolContainer = containerObj.transform;
        }

        if (!pools.TryGetValue(prefab, out Queue<Bullet> queue))
        {
            queue = new Queue<Bullet>();
            pools[prefab] = queue;
        }

        Bullet bulletInstance = null;
        while (queue.Count > 0 && bulletInstance == null)
        {
            bulletInstance = queue.Dequeue();
        }

        if (bulletInstance == null)
        {
            GameObject obj = Instantiate(prefab, position, rotation, poolContainer);
            bulletInstance = obj.GetComponent<Bullet>();
            if (bulletInstance == null)
            {
                bulletInstance = obj.AddComponent<Bullet>();
            }
            bulletInstance.sourcePrefab = prefab;
            bulletInstance.defaultLocalScale = obj.transform.localScale;
            bulletInstance.scaleRecorded = true;
        }
        else
        {
            if (!bulletInstance.scaleRecorded)
            {
                bulletInstance.defaultLocalScale = bulletInstance.transform.localScale;
                bulletInstance.scaleRecorded = true;
            }
            bulletInstance.transform.SetPositionAndRotation(position, rotation);
            bulletInstance.gameObject.SetActive(true);
        }

        bulletInstance.transform.localScale = bulletInstance.defaultLocalScale * scaleMultiplier;
        bulletInstance.isAreaClusterBullet = isAreaCluster;
        bulletInstance.areaExplosionRadius = explosionRadius;

        if (customDamage > 0f)
        {
            bulletInstance.SetDamage(customDamage);
        }

        bulletInstance.ActivateBullet(customLifetime);
        return bulletInstance;
    }

    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    void OnEnable()
    {
        if (!scaleRecorded)
        {
            defaultLocalScale = transform.localScale;
            scaleRecorded = true;
        }
        ActivateBullet(-1f);
    }

    private void ActivateBullet(float customLifetime)
    {
        float life = customLifetime > 0f ? customLifetime : lifetime;
        deactivateTime = Time.time + life;
    }

    void Update()
    {
        if (Time.time >= deactivateTime)
        {
            ReturnToPool();
            return;
        }

        float step = speed * Time.deltaTime;

        // 1. Verificar colisión inmediata en la posición actual
        Collider[] overlaps = Physics.OverlapSphere(transform.position, hitRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (TryProcessHit(overlaps[i])) return;
        }

        // 2. SphereCast continuo a lo largo del avance del frame para nunca atravesar enemigos
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, hitRadius, transform.forward, step, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryProcessHit(hits[i].collider)) return;
        }

        transform.position += transform.forward * step;

        // 3. Si es una bala pesada de área (Torreta 1) y toca el nivel del suelo, detonar en esquirlas
        if (isAreaClusterBullet && transform.position.y <= 0.6f)
        {
            DetonateAreaCluster(transform.position);
            ReturnToPool();
        }
    }

    private bool TryProcessHit(Collider other)
    {
        if (other == null || other.gameObject == gameObject) return false;

        if (other.CompareTag("Player") ||
            other.GetComponentInParent<PlayerMove>() != null ||
            other.GetComponentInParent<WagonController>() != null ||
            other.GetComponentInParent<Turret>() != null ||
            other.GetComponentInParent<TurretSlotMarker>() != null ||
            other.GetComponent<Bullet>() != null)
        {
            return false;
        }

        EnemyFSM enemy = other.GetComponentInParent<EnemyFSM>();
        if (enemy == null)
        {
            enemy = other.GetComponentInChildren<EnemyFSM>();
        }

        if (enemy != null)
        {
            if (isAreaClusterBullet)
            {
                DetonateAreaCluster(enemy.GetTargetCenter());
            }
            else
            {
                enemy.TakeDamage(damage);
            }
            ReturnToPool();
            return true;
        }

        return false;
    }

    private void DetonateAreaCluster(Vector3 impactPoint)
    {
        // Aplicar daño en toda la zona de explosión
        Collider[] hits = Physics.OverlapSphere(impactPoint, areaExplosionRadius, ~0, QueryTriggerInteraction.Collide);
        HashSet<EnemyFSM> damagedEnemies = new HashSet<EnemyFSM>();

        foreach (Collider hit in hits)
        {
            EnemyFSM e = hit.GetComponentInParent<EnemyFSM>();
            if (e != null && !damagedEnemies.Contains(e))
            {
                damagedEnemies.Add(e);
                e.TakeDamage(damage);
            }
        }

        // Disparar un anillo de 8 balas medianas/pequeñas (esquirlas) en 360 grados para ver claramente el efecto de área
        GameObject prefabToUse = sourcePrefab != null ? sourcePrefab : GetOrCreateFallbackPrefab();
        float angleStep = 360f / shrapnelCount;
        Vector3 spawnOrigin = new Vector3(impactPoint.x, Mathf.Max(0.8f, impactPoint.y), impactPoint.z);

        for (int i = 0; i < shrapnelCount; i++)
        {
            float angle = i * angleStep;
            Quaternion rot = Quaternion.Euler(0f, angle, 0f);
            SpawnFromPool(
                prefabToUse,
                spawnOrigin,
                rot,
                customDamage: damage * 0.35f,
                isAreaCluster: false,
                explosionRadius: 0f,
                scaleMultiplier: 0.75f,
                customLifetime: 0.28f
            );
        }
    }

    private void ReturnToPool()
    {
        isAreaClusterBullet = false;
        if (scaleRecorded)
        {
            transform.localScale = defaultLocalScale;
        }

        if (sourcePrefab != null && pools.TryGetValue(sourcePrefab, out Queue<Bullet> queue))
        {
            gameObject.SetActive(false);
            queue.Enqueue(this);
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}