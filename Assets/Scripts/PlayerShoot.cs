using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.15f;
    [SerializeField] private float bulletDamage = 25f;

    [Header("Ammo")]
    [SerializeField] private int magSize = 20;
    [SerializeField] private float reloadTime = 1.5f;

    private int currentAmmo;
    private bool isReloading;
    private float reloadEndTime;
    private float nextFireTime;
    private Camera cam;
    private Vector3 currentAimPoint;

    void Start()
    {
        cam = Camera.main;
        currentAmmo = magSize;
    }

    void Update()
    {
        if (cam == null) cam = Camera.main;
        RotateAndAimToMouse();

        if (isReloading)
        {
            if (Time.time >= reloadEndTime) FinishReload();
            return;
        }

        if (Input.GetKeyDown(KeyCode.X) && currentAmmo < magSize)
        {
            Reload();
            return;
        }

        // Evitar disparar cuando el jugador hace clic sobre el HUD de cartas (esquina superior izquierda)
        bool mouseOverHud = Input.mousePosition.x < 880f && (Screen.height - Input.mousePosition.y) < 250f;

        if (Input.GetMouseButton(0) && !mouseOverHud && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0) Shoot();
            else Reload();
        }
    }

    void RotateAndAimToMouse()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        bool foundTargetPoint = false;

        // 1. Primero verificar si el mouse apunta directamente a un enemigo o al piso en 3D
        RaycastHit[] hits = Physics.RaycastAll(ray, 200f, ~0, QueryTriggerInteraction.Collide);
        foreach (RaycastHit hit in hits)
        {
            EnemyFSM enemy = hit.collider.GetComponentInParent<EnemyFSM>();
            if (enemy != null)
            {
                currentAimPoint = enemy.GetTargetCenter();
                foundTargetPoint = true;
                break;
            }
        }

        // 2. Si no apuntó directo al collider del enemigo, proyectar sobre el plano a la altura del pecho de los enemigos (Y = 1.0f)
        if (!foundTargetPoint)
        {
            Plane groundAimPlane = new Plane(Vector3.up, new Vector3(0f, 1.0f, 0f));
            if (groundAimPlane.Raycast(ray, out float distance))
            {
                currentAimPoint = ray.GetPoint(distance);

                // Asistencia suave: si cerca del cursor hay un enemigo, ajustar la altura Y al centro de ese enemigo
                EnemyFSM closestNearCursor = FindEnemyNearPoint(currentAimPoint, 2.2f);
                if (closestNearCursor != null)
                {
                    currentAimPoint = closestNearCursor.GetTargetCenter();
                }
                foundTargetPoint = true;
            }
        }

        if (foundTargetPoint)
        {
            // Rotar la cápsula del Player solo en horizontal (Y = 0) para que no se incline hacia el suelo
            Vector3 flatDir = currentAimPoint - transform.position;
            flatDir.y = 0f;
            if (flatDir.sqrMagnitude > 0.001f)
            {
                transform.rotation = Quaternion.LookRotation(flatDir);
            }
        }
    }

    private EnemyFSM FindEnemyNearPoint(Vector3 point, float maxRadius)
    {
        EnemyFSM best = null;
        float minDist = maxRadius;
        foreach (EnemyFSM enemy in FindObjectsByType<EnemyFSM>(FindObjectsSortMode.None))
        {
            Vector3 ePos = enemy.GetTargetCenter();
            float distXZ = Vector2.Distance(new Vector2(point.x, point.z), new Vector2(ePos.x, ePos.z));
            if (distXZ < minDist)
            {
                minDist = distXZ;
                best = enemy;
            }
        }
        return best;
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        Transform spawn = firePoint != null ? firePoint : transform;

        // Calcular la rotación 3D real (incluyendo la inclinación en Y hacia abajo) desde el FirePoint hacia el objetivo
        Vector3 aimDir3D = currentAimPoint - spawn.position;
        Quaternion shotRotation = aimDir3D.sqrMagnitude > 0.001f
            ? Quaternion.LookRotation(aimDir3D.normalized)
            : spawn.rotation;

        Bullet.SpawnFromPool(bulletPrefab, spawn.position, shotRotation, bulletDamage);
    }

    void Reload()
    {
        isReloading = true;
        reloadEndTime = Time.time + reloadTime;
        Debug.Log("Reloading...");
    }

    void FinishReload()
    {
        currentAmmo = magSize;
        isReloading = false;
        Debug.Log("Reloaded");
    }
}