using UnityEngine;

public class BulletTrack : MonoBehaviour
{
    [SerializeField] private float turnSpeed = 360f;

    private EnemyFSM enemy;

    public void TrackTarget(Transform target)
    {
        enemy = target != null ? target.GetComponent<EnemyFSM>() : null;
    }

    public void Steer()
    {
        if (enemy == null || !enemy.gameObject.activeInHierarchy)
        {
            enemy = null;
            return;
        }

        Vector3 direction = enemy.GetTargetCenter() - transform.position;

        if (direction.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            Mathf.Max(0f, turnSpeed) * Time.deltaTime
        );
    }

    private void OnDisable()
    {
        enemy = null;
    }
}