using UnityEngine;
using UnityEngine.AI;

public enum EnemyStates
{
    Chase,
    Attack,
    Flee
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyFSM : MonoBehaviour
{
    [Header("Combate y Navegación")]
    [SerializeField] private float attackRange = 2.4f;
    [SerializeField] private float attackDamagePerSecond = 12f;
    [SerializeField] private float fleeDistance = 8f;

    [Header("Vida del Enemigo")]
    [SerializeField] private float maxHealth = 50f;
    [SerializeField] private float currentHealth;

    public EnemyStates currentState = EnemyStates.Chase;
    public Transform target;

    private WagonController targetWagon;
    private Collider myCollider;
    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        myCollider = GetComponentInChildren<Collider>();
        currentHealth = maxHealth;
    }

    void Start()
    {
        ChangeTarget();
    }

    public Vector3 GetTargetCenter()
    {
        if (myCollider != null)
        {
            return myCollider.bounds.center;
        }
        return transform.position + Vector3.up * 1.0f;
    }

    void Update()
    {
        // Si el vagón objetivo fue destruido o desacoplado, buscar otro vagón inmediatamente
        if (target == null || !target.gameObject.activeInHierarchy || targetWagon == null)
        {
            ChangeTarget();
            if (target == null)
            {
                if (agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
        }

        // Calcular el punto más cercano del borde real del vagón (usando sus Bounds reales aunque estén en el hijo PisoVagon)
        Bounds wagonBounds = targetWagon.GetWorldBounds();
        Vector3 closestPoint = wagonBounds.ClosestPoint(transform.position);

        float distanceToSurface = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(closestPoint.x, closestPoint.z)
        );

        switch (currentState)
        {
            case EnemyStates.Chase:
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    agent.SetDestination(closestPoint);
                }
                if (distanceToSurface <= attackRange)
                {
                    currentState = EnemyStates.Attack;
                }
                break;

            case EnemyStates.Attack:
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                // Mirar hacia el vagón
                Vector3 look = closestPoint - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(look);

                // Aplicar daño continuo al vagón
                if (targetWagon != null)
                {
                    targetWagon.TakeDamage(attackDamagePerSecond * Time.deltaTime);
                }

                if (distanceToSurface > attackRange + 0.6f)
                {
                    currentState = EnemyStates.Chase;
                }
                break;

            case EnemyStates.Flee:
                if (agent.isOnNavMesh)
                {
                    agent.isStopped = false;
                    Vector3 awayDir = (transform.position - closestPoint).normalized;
                    Vector3 fleePoint = transform.position + awayDir * fleeDistance;

                    if (NavMesh.SamplePosition(fleePoint, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
                        agent.SetDestination(hit.position);
                }
                break;
        }
    }

    public void ChangeTarget()
    {
        if (GameManager.Instance != null)
        {
            WagonController closestWagon = GameManager.Instance.GetClosestActiveWagon(transform.position);
            if (closestWagon != null)
            {
                target = closestWagon.transform;
                targetWagon = closestWagon;
                currentState = EnemyStates.Chase;
            }
        }
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        Debug.Log($"[Enemy] Recibió {amount} de daño. Vida restante: {currentHealth}/{maxHealth}");
        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
        }
    }

    public void ChangeState(EnemyStates newState)
    {
        currentState = newState;
    }
}