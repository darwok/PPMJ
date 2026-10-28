using UnityEngine;

public class PlayerShoot : MonoBehaviour
{
    [Header("Shooting")]
    [SerializeField] private GameObject bulletPrefab;
    [SerializeField] private Transform firePoint;
    [SerializeField] private float fireRate = 0.15f;

    [Header("Ammo")]
    [SerializeField] private int magSize = 20;
    [SerializeField] private float reloadTime = 1.5f;

    private int currentAmmo;
    private bool isReloading;
    private float reloadEndTime;
    private float nextFireTime;
    private Camera cam;

    void Start()
    {
        cam = Camera.main;
        currentAmmo = magSize;
    }

    void Update()
    {
        RotateToMouse();

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

        if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
        {
            if (currentAmmo > 0) Shoot();
            else Reload(); // auto reload when empty
        }
    }

    void RotateToMouse()
    {
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 dir = ray.GetPoint(distance) - transform.position;
            dir.y = 0f;

            if (dir.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(dir);
        }
    }

    void Shoot()
    {
        nextFireTime = Time.time + fireRate;
        currentAmmo--;

        Transform spawn = firePoint != null ? firePoint : transform;
        Instantiate(bulletPrefab, spawn.position, spawn.rotation);

        Debug.Log($"Ammo: {currentAmmo}/{magSize}");
    }

    // Regular void: starts the reload and records when it should end
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