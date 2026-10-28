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
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float fleeDistance = 8f;

    public EnemyStates currentState = EnemyStates.Chase;
    public Transform target;

    private NavMeshAgent agent;

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        ChangeTarget();
    }

    void Update()
    {
        if (target == null) return;

        float distance = Vector3.Distance(transform.position, target.position);

        switch (currentState)
        {
            case EnemyStates.Chase:
                agent.isStopped = false;
                agent.SetDestination(target.position);
                if (distance <= attackRange) currentState = EnemyStates.Attack;
                break;

            case EnemyStates.Attack:
                agent.isStopped = true;
                // Face the target
                Vector3 look = target.position - transform.position;
                look.y = 0f;
                if (look != Vector3.zero)
                    transform.rotation = Quaternion.LookRotation(look);
                // Put attack logic here
                if (distance > attackRange) currentState = EnemyStates.Chase;
                break;

            case EnemyStates.Flee:
                agent.isStopped = false;
                Vector3 awayDir = (transform.position - target.position).normalized;
                Vector3 fleePoint = transform.position + awayDir * fleeDistance;

                if (NavMesh.SamplePosition(fleePoint, out NavMeshHit hit, fleeDistance, NavMesh.AllAreas))
                    agent.SetDestination(hit.position);
                break;
        }
    }

    public void ChangeTarget()
    {
        if (GameManager.Instance != null)
        {
            target = GameManager.Instance.currentTarget;
        }
    }

    public void ChangeState(EnemyStates newState)
    {
        currentState = newState;
    }
}
