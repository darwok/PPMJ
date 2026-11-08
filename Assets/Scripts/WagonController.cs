using UnityEngine;

public class WagonController : MonoBehaviour
{
    [Header("Configuración del Vagón")]
    [Tooltip("Marcar TRUE solo en 'Locomotive' (la punta del tren que no lleva torretas)")]
    [SerializeField] private bool isLocomotive = false;
    [SerializeField] private float maxHealth = 150f;
    [SerializeField] private float currentHealth;

    [Header("Alineación Automática en Z")]
    [Tooltip("Fuerza que el vagón y su malla hija PisoVagon queden centrados en Z = 0")]
    [SerializeField] private float targetZPosition = 0f;

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

        // Solo es locomotora si su nombre contiene "locomot" o "ejemplo"
        if (name.ToLower().Contains("locomot") || name.ToLower().Contains("ejemplo"))
        {
            isLocomotive = true;
        }
        else
        {
            isLocomotive = false;
        }

        AlignChildMeshToZ(targetZPosition);

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

    public void SetAsCargoWagon()
    {
        isLocomotive = false;
    }

    // Asegura que tanto el objeto padre como su hijo PisoVagon estén alineados en Z = 0 sin el desfase de -3.42
    public void AlignChildMeshToZ(float targetZ)
    {
        Vector3 p = transform.position;
        p.z = targetZ;
        transform.position = p;

        foreach (Transform child in transform)
        {
            if (child.GetComponent<TurretSlotMarker>() != null || child.GetComponent<Turret>() != null)
                continue;

            Vector3 lp = child.localPosition;
            if (Mathf.Abs(lp.z) > 0.01f)
            {
                lp.z = 0f;
                child.localPosition = lp;
            }
        }
    }

    void Update()
    {
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
        MeshFilter[] meshFilters = GetComponentsInChildren<MeshFilter>();
        bool hasBounds = false;
        Bounds combined = new Bounds(transform.position, new Vector3(10f, 5f, 5f));

        foreach (MeshFilter mf in meshFilters)
        {
            if (mf == null || mf.sharedMesh == null) continue;
            if (mf.GetComponent<TurretSlotMarker>() != null || mf.GetComponentInParent<Turret>() != null)
                continue;

            Transform t = mf.transform;
            Bounds localB = mf.sharedMesh.bounds;
            Vector3 worldCenter = t.TransformPoint(localB.center);
            Vector3 worldSize = Vector3.Scale(localB.size, t.lossyScale);
            worldSize = new Vector3(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y), Mathf.Abs(worldSize.z));

            Bounds worldB = new Bounds(worldCenter, worldSize);
            if (!hasBounds)
            {
                combined = worldB;
                hasBounds = true;
            }
            else
            {
                combined.Encapsulate(worldB);
            }
        }

        return combined;
    }

    public void CreateOrRefreshRoofSlots()
    {
        TurretSlotMarker[] existingMarkers = GetComponentsInChildren<TurretSlotMarker>();
        for (int i = 0; i < existingMarkers.Length; i++)
        {
            if (Application.isPlaying) Destroy(existingMarkers[i].gameObject);
            else DestroyImmediate(existingMarkers[i].gameObject);
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