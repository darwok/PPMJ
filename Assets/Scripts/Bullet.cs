using System.Collections.Generic;
using UnityEngine;

public class Bullet : MonoBehaviour
{
    [Header("Ajustes del Proyectil")]
    [SerializeField] private float speed = 28f;
    [SerializeField] private float lifetime = 3f;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float hitRadius = 0.45f;

    private float deactivateTime;
    private GameObject sourcePrefab;

    // Sistema de Object Pooling integrado para no usar Instantiate/Destroy en cada disparo
    private static readonly Dictionary<GameObject, Queue<Bullet>> pools = new Dictionary<GameObject, Queue<Bullet>>();
    private static Transform poolContainer;

    public static Bullet SpawnFromPool(GameObject prefab, Vector3 position, Quaternion rotation, float customDamage = -1f)
    {
        if (prefab == null) return null;

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
        }
        else
        {
            bulletInstance.transform.SetPositionAndRotation(position, rotation);
            bulletInstance.gameObject.SetActive(true);
        }

        if (customDamage > 0f)
        {
            bulletInstance.SetDamage(customDamage);
        }

        bulletInstance.ActivateBullet();
        return bulletInstance;
    }

    public void SetDamage(float newDamage)
    {
        damage = newDamage;
    }

    void OnEnable()
    {
        ActivateBullet();
    }

    private void ActivateBullet()
    {
        deactivateTime = Time.time + lifetime;
    }

    void Update()
    {
        if (Time.time >= deactivateTime)
        {
            ReturnToPool();
            return;
        }

        float step = speed * Time.deltaTime;

        // 1. Verificar si ya está tocando un enemigo en la posición inicial
        Collider[] overlaps = Physics.OverlapSphere(transform.position, hitRadius, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < overlaps.Length; i++)
        {
            if (TryProcessHit(overlaps[i])) return;
        }

        // 2. SphereCast continuo a lo largo del desplazamiento del frame para que NUNCA atraviese enemigos
        RaycastHit[] hits = Physics.SphereCastAll(transform.position, hitRadius, transform.forward, step, ~0, QueryTriggerInteraction.Collide);
        for (int i = 0; i < hits.Length; i++)
        {
            if (TryProcessHit(hits[i].collider)) return;
        }

        transform.position += transform.forward * step;
    }

    private bool TryProcessHit(Collider other)
    {
        if (other == null || other.gameObject == gameObject) return false;

        // Ignorar colisiones con el Player, Vagones, Nodos de Torreta, Torretas u otras Balas
        if (other.CompareTag("Player") ||
            other.GetComponentInParent<PlayerMove>() != null ||
            other.GetComponentInParent<WagonController>() != null ||
            other.GetComponentInParent<Turret>() != null ||
            other.GetComponentInParent<TurretSlotMarker>() != null ||
            other.GetComponent<Bullet>() != null)
        {
            return false;
        }

        // Buscar EnemyFSM tanto en el objeto golpeado como en sus padres o hijos
        EnemyFSM enemy = other.GetComponentInParent<EnemyFSM>();
        if (enemy == null)
        {
            enemy = other.GetComponentInChildren<EnemyFSM>();
        }

        if (enemy != null)
        {
            enemy.TakeDamage(damage);
            ReturnToPool();
            return true;
        }

        return false;
    }

    private void ReturnToPool()
    {
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