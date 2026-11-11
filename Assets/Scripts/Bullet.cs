using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Ajustes del Proyectil")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float hitRadius = 0.45f;

    private bool isAreaClusterBullet;
    private float areaExplosionRadius;
    private int shrapnelCount = 8;
    private float knockbackForce = 0f;

    private Vector3 defaultLocalScale;
    private bool scaleRecorded;

    private float deactivateTime;
    private GameObject sourcePrefab;

    private static readonly Dictionary<GameObject, Queue<Bullet>> pools =
        new Dictionary<GameObject, Queue<Bullet>>();

    private static Transform poolContainer;
    private static GameObject fallbackBulletPrefab;

    public static GameObject GetOrCreateFallbackPrefab()
    {
        if (fallbackBulletPrefab == null)
        {
            fallbackBulletPrefab = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            fallbackBulletPrefab.name = "DefaultPooledBullet_Prefab";
            fallbackBulletPrefab.transform.localScale =
                new Vector3(0.35f, 0.35f, 0.35f);

            Collider collider = fallbackBulletPrefab.GetComponent<Collider>();
            if (collider != null)
                collider.isTrigger = true;

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
        float customLifetime = -1f,
        EnemyFSM homingTarget = null,
        float customKnockbackForce = 0f)
    {
        if (prefab == null)
            prefab = GetOrCreateFallbackPrefab();

        if (poolContainer == null)
        {
            GameObject containerObject = new GameObject("_BulletPool_Container");
            poolContainer = containerObject.transform;
        }

        if (!pools.TryGetValue(prefab, out Queue<Bullet> queue))
        {
            queue = new Queue<Bullet>();
            pools[prefab] = queue;
        }

        Bullet bulletInstance = null;

        while (queue.Count > 0 && bulletInstance == null)
            bulletInstance = queue.Dequeue();

        if (bulletInstance == null)
        {
            GameObject bulletObject = Instantiate(
                prefab,
                position,
                rotation,
                poolContainer
            );

            bulletInstance = bulletObject.GetComponent<Bullet>();
            if (bulletInstance == null)
                bulletInstance = bulletObject.AddComponent<Bullet>();

            bulletInstance.sourcePrefab = prefab;
            bulletInstance.defaultLocalScale = bulletObject.transform.localScale;
            bulletInstance.scaleRecorded = true;
        }

        if (!bulletInstance.scaleRecorded)
        {
            bulletInstance.defaultLocalScale = bulletInstance.transform.localScale;
            bulletInstance.scaleRecorded = true;
        }

        bulletInstance.transform.SetPositionAndRotation(position, rotation);
        bulletInstance.transform.localScale =
            bulletInstance.defaultLocalScale * scaleMultiplier;

        bulletInstance.isAreaClusterBullet = isAreaCluster;
        bulletInstance.areaExplosionRadius = explosionRadius;
        bulletInstance.sourcePrefab = prefab;
        bulletInstance.knockbackForce = customKnockbackForce;

        if (customDamage >= 0f)
            bulletInstance.SetDamage(customDamage);

        BulletTrack tracker = bulletInstance.GetComponent<BulletTrack>();

        if (homingTarget != null && tracker == null)
            tracker = bulletInstance.gameObject.AddComponent<BulletTrack>();

        if (tracker != null)
            tracker.TrackTarget(homingTarget != null ? homingTarget.transform : null);

        if (!bulletInstance.gameObject.activeSelf)
            bulletInstance.gameObject.SetActive(true);

        bulletInstance.ActivateBullet(customLifetime);
        return bulletInstance;
    }

    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    private void OnEnable()
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
        float bulletLifetime = customLifetime > 0f ? customLifetime : lifetime;
        deactivateTime = Time.time + bulletLifetime;
    }

    private void Update()
    {
        if (Time.time >= deactivateTime)
        {
            ReturnToPool();
            return;
        }

        BulletTrack tracker = GetComponent<BulletTrack>();
        if (tracker != null)
            tracker.Steer();

        float step = speed * Time.deltaTime;

        Collider[] overlaps = Physics.OverlapSphere(
            transform.position,
            hitRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < overlaps.Length; i++)
        {
            if (TryProcessHit(overlaps[i]))
                return;
        }

        RaycastHit[] hits = Physics.SphereCastAll(
            transform.position,
            hitRadius,
            transform.forward,
            step,
            ~0,
            QueryTriggerInteraction.Collide
        );

        for (int i = 0; i < hits.Length; i++)
        {
            if (TryProcessHit(hits[i].collider))
                return;
        }

        transform.position += transform.forward * step;

        if (isAreaClusterBullet && transform.position.y <= 0.6f)
        {
            DetonateAreaCluster(transform.position);
            ReturnToPool();
        }
    }

    private bool TryProcessHit(Collider other)
    {
        if (other == null || other.gameObject == gameObject)
            return false;

        if (other.CompareTag("Player") ||
            other.GetComponentInParent<PlayerMove>() != null ||
            other.GetComponentInParent<WagonController>() != null ||
            other.GetComponentInParent<Turret>() != null ||
            other.GetComponentInParent<TurretFlow>() != null ||
            other.GetComponentInParent<TurretSlotMarker>() != null ||
            other.GetComponentInParent<Bullet>() != null)
        {
            return false;
        }

        EnemyFSM enemy = other.GetComponentInParent<EnemyFSM>();

        if (enemy == null)
            enemy = other.GetComponentInChildren<EnemyFSM>();

        if (enemy == null)
            return false;

        if (knockbackForce > 0f)
        {
            enemy.ApplyKnockback(transform.forward, knockbackForce);
        }

        if (isAreaClusterBullet)
            DetonateAreaCluster(enemy.GetTargetCenter());
        else if (damage > 0f)
            enemy.TakeDamage(damage);

        ReturnToPool();
        return true;
    }

    private void DetonateAreaCluster(Vector3 impactPoint)
    {
        Collider[] hits = Physics.OverlapSphere(
            impactPoint,
            areaExplosionRadius,
            ~0,
            QueryTriggerInteraction.Collide
        );

        HashSet<EnemyFSM> damagedEnemies = new HashSet<EnemyFSM>();

        foreach (Collider hit in hits)
        {
            EnemyFSM enemy = hit.GetComponentInParent<EnemyFSM>();

            if (enemy != null && damagedEnemies.Add(enemy))
                enemy.TakeDamage(damage);
        }

        GameObject prefabToUse = sourcePrefab != null
            ? sourcePrefab
            : GetOrCreateFallbackPrefab();

        float angleStep = 360f / shrapnelCount;
        Vector3 spawnOrigin = new Vector3(
            impactPoint.x,
            Mathf.Max(0.8f, impactPoint.y),
            impactPoint.z
        );

        for (int i = 0; i < shrapnelCount; i++)
        {
            float angle = i * angleStep;
            Quaternion rotation = Quaternion.Euler(0f, angle, 0f);

            SpawnFromPool(
                prefabToUse,
                spawnOrigin,
                rotation,
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
        knockbackForce = 0f;

        BulletTrack tracker = GetComponent<BulletTrack>();
        if (tracker != null)
            tracker.TrackTarget(null);

        if (scaleRecorded)
            transform.localScale = defaultLocalScale;

        gameObject.SetActive(false);

        if (sourcePrefab != null &&
            pools.TryGetValue(sourcePrefab, out Queue<Bullet> queue))
        {
            queue.Enqueue(this);
        }
    }
}