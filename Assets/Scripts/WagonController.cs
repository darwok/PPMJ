using UnityEngine;

public class WagonController : MonoBehaviour
{
    [Header("Configuración del Vagón")]
    [Tooltip("Marcar TRUE solo en 'Locomotive' (la punta del tren que no lleva torretas)")]
    [SerializeField] private bool isLocomotive = false;
    [SerializeField] private float maxHealth = 150f;
    [SerializeField] private float currentHealth;

    [Header("Ajuste de Altura de Torretas en el Inspector")]
    [Tooltip("Modifica este valor en el Inspector (incluso en Play Mode) para subir o bajar la altura Y de las torretas sobre el vagón")]
    public float turretHeightOffset = 1.6f;

    [Header("2 Nodos de Colocación de Torretas en el Techo (Izquierdo y Derecho)")]
    public Transform[] turretSlots = new Transform[2];
    private Turret[] installedTurrets = new Turret[2];

    public bool IsLocomotive => isLocomotive;
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;

    void Awake()
    {
        currentHealth = maxHealth;
        installedTurrets = new Turret[2];

        if (name.ToLower().Contains("locomot") || name.ToLower().Contains("ejemplo"))
        {
            isLocomotive = true;
        }

        if (!isLocomotive)
        {
            CreateOrRefreshRoofSlots();
        }
    }

    void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterWagon(this);
        }
    }

    void Update()
    {
        // Mantener actualizada en tiempo real la altura de las torretas por si ajustas turretHeightOffset en el Inspector mientras juegas
        if (installedTurrets != null && turretSlots != null)
        {
            for (int i = 0; i < installedTurrets.Length; i++)
            {
                if (installedTurrets[i] != null && turretSlots[i] != null)
                {
                    installedTurrets[i].transform.position = turretSlots[i].position + Vector3.up * turretHeightOffset;
                }
            }
        }
    }

    public Bounds GetWorldBounds()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>();
        bool hasBounds = false;
        Bounds combined = new Bounds(transform.position, new Vector3(10f, 5f, 5f));

        foreach (Renderer r in renderers)
        {
            if (r.GetComponent<TurretSlotMarker>() != null || r.GetComponentInParent<Turret>() != null)
                continue;

            if (!hasBounds)
            {
                combined = r.bounds;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(r.bounds);
            }
        }
        return combined;
    }

    public void CreateOrRefreshRoofSlots()
    {
        TurretSlotMarker[] existingMarkers = GetComponentsInChildren<TurretSlotMarker>();
        for (int i = 0; i < existingMarkers.Length; i++)
        {
            Destroy(existingMarkers[i].gameObject);
        }

        turretSlots = new Transform[2];
        installedTurrets = new Turret[2];

        Bounds b = GetWorldBounds();
        float quarterLengthX = b.extents.x * 0.5f;
        float[] xPositions = new float[] { b.center.x - quarterLengthX, b.center.x + quarterLengthX };
        float roofTopY = b.max.y + 0.08f;

        for (int i = 0; i < 2; i++)
        {
            GameObject padObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            padObj.name = $"TurretSlot_{i + 1}";
            padObj.transform.position = new Vector3(xPositions[i], roofTopY, b.center.z);
            padObj.transform.rotation = Quaternion.identity;
            padObj.transform.localScale = new Vector3(b.size.x * 0.38f, 0.15f, b.size.z * 0.7f);
            padObj.transform.SetParent(transform, true);

            Renderer padRenderer = padObj.GetComponent<Renderer>();
            if (padRenderer != null)
            {
                padRenderer.material.color = new Color(0.22f, 0.28f, 0.34f, 1f);
            }

            BoxCollider col = padObj.GetComponent<BoxCollider>();
            if (col != null)
            {
                col.isTrigger = false;
            }

            TurretSlotMarker marker = padObj.AddComponent<TurretSlotMarker>();
            marker.Setup(this, i);

            turretSlots[i] = padObj.transform;
        }
    }

    public int GetClosestSlotIndex(Vector3 worldPoint)
    {
        Bounds b = GetWorldBounds();
        return (worldPoint.x < b.center.x) ? 0 : 1;
    }

    public bool PlaceTurret(int slotIndex, GameObject turretPrefab, bool replaceExisting = true)
    {
        if (isLocomotive || slotIndex < 0 || slotIndex >= turretSlots.Length || turretPrefab == null)
            return false;

        if (installedTurrets[slotIndex] != null)
        {
            if (!replaceExisting) return false;
            Destroy(installedTurrets[slotIndex].gameObject);
        }

        Transform slotTransform = turretSlots[slotIndex];
        Vector3 spawnPos = slotTransform.position + Vector3.up * turretHeightOffset;
        GameObject newTurretObj = Instantiate(turretPrefab, spawnPos, Quaternion.identity, transform);
        Turret turretComp = newTurretObj.GetComponent<Turret>();
        if (turretComp == null)
        {
            turretComp = newTurretObj.AddComponent<Turret>();
        }
        installedTurrets[slotIndex] = turretComp;

        Debug.Log($"[Vagón {name}] Torreta {turretPrefab.name} colocada en el espacio {slotIndex + 1}/2 (Altura Y Offset: {turretHeightOffset}).");
        return true;
    }

    public void TakeDamage(float amount)
    {
        currentHealth -= amount;
        if (currentHealth <= 0f)
        {
            DestroyOrDetachWagon();
        }
    }

    public void HealWagon(float amount)
    {
        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
    }

    public void DestroyOrDetachWagon()
    {
        if (installedTurrets != null)
        {
            for (int i = 0; i < installedTurrets.Length; i++)
            {
                if (installedTurrets[i] != null)
                {
                    Destroy(installedTurrets[i].gameObject);
                }
            }
        }

        PlayerMove player = FindFirstObjectByType<PlayerMove>();
        if (player != null && GameManager.Instance != null)
        {
            Bounds myBounds = GetWorldBounds();
            if (Vector3.Distance(player.transform.position, myBounds.center) < myBounds.extents.x + 3f)
            {
                foreach (var w in GameManager.Instance.activeWagons)
                {
                    if (w != null && w != this)
                    {
                        Bounds targetBounds = w.GetWorldBounds();
                        player.transform.position = new Vector3(targetBounds.center.x, targetBounds.max.y + 1.2f, targetBounds.center.z);
                        break;
                    }
                }
            }
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnWagonRemoved(this, isLocomotive);
        }

        Destroy(gameObject);
    }
}

public class TurretSlotMarker : MonoBehaviour
{
    public WagonController ParentWagon { get; private set; }
    public int SlotIndex { get; private set; }

    public void Setup(WagonController wagon, int index)
    {
        ParentWagon = wagon;
        SlotIndex = index;
    }
}