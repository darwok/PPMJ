using UnityEngine;
using UnityEngine.AI;

public enum EnemyStates
{
    Chase,
    Attack,
    Flee
}

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

    // Si este EnemyFSM pertenece al Tren Enemigo paralelo, redirige todo el daño al EnemyTrainController
    private bool isEnemyTrainBossProxy = false;
    private EnemyTrainController enemyTrainBoss;

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
        if (!isEnemyTrainBossProxy)
        {
            ChangeTarget();
        }
    }

    public void ConfigureAsEnemyTrainTarget(EnemyTrainController bossController)
    {
        isEnemyTrainBossProxy = true;
        enemyTrainBoss = bossController;
        myCollider = GetComponent<Collider>();
        if (agent != null) agent.enabled = false;
    }

    public Vector3 GetTargetCenter()
    {
        if (myCollider != null)
        {
            return myCollider.bounds.center;
        }
        return transform.position + Vector3.up * 1.2f;
    }

    void Update()
    {
        if (isEnemyTrainBossProxy) return;

        if (target == null || !target.gameObject.activeInHierarchy || targetWagon == null)
        {
            ChangeTarget();
            if (target == null)
            {
                if (agent != null && agent.isOnNavMesh) agent.isStopped = true;
                return;
            }
        }

        Bounds wagonBounds = targetWagon.GetWorldBounds();
        Vector3 closestPoint = wagonBounds.ClosestPoint(transform.position);

        float distanceToSurface = Vector2.Distance(
            new Vector2(transform.position.x, transform.position.z),
            new Vector2(closestPoint.x, closestPoint.z)
        );

        switch (currentState)
        {
            case EnemyStates.Chase:
                if (agent != null && agent.isOnNavMesh)
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
                if (agent != null && agent.isOnNavMesh)
                {
                    agent.isStopped = true;
                }

                Vector3 look = closestPoint - transform.position;
                look.y = 0f;
                if (look.sqrMagnitude > 0.001f)
                    transform.rotation = Quaternion.LookRotation(look);

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
                if (agent != null && agent.isOnNavMesh)
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
        if (isEnemyTrainBossProxy) return;

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
        if (isEnemyTrainBossProxy && enemyTrainBoss != null)
        {
            enemyTrainBoss.ReceiveDamageFromBullet(amount);
            return;
        }

        if (amount <= 0f) return;

        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            Destroy(gameObject);
        }
    }

    // Empuje hacia atrás provocado por la Torreta 5 de Agua (Water Hose)
    public void ApplyKnockback(Vector3 pushDirection, float pushForce)
    {
        if (isEnemyTrainBossProxy || pushForce <= 0f) return;

        pushDirection.y = 0f;
        if (pushDirection.sqrMagnitude < 0.001f)
        {
            pushDirection = -transform.forward;
        }
        pushDirection.Normalize();

        Vector3 pushedPos = transform.position + pushDirection * pushForce;
        if (agent != null && agent.isOnNavMesh)
        {
            if (NavMesh.SamplePosition(pushedPos, out NavMeshHit hit, pushForce + 1f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
            }
        }
        else
        {
            transform.position = pushedPos;
        }
    }

    public void ChangeState(EnemyStates newState)
    {
        currentState = newState;
    }
}