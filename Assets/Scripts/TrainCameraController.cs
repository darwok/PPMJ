using UnityEngine;

public class TrainCameraController : MonoBehaviour
{
    public static TrainCameraController Instance { get; private set; }

    [Header("Objetivo a Seguir (Modo Cercano)")]
    [Tooltip("Arrastra aquí al objeto Player (si lo dejas vacío, lo busca automáticamente)")]
    [SerializeField] private Transform playerTarget;
    [SerializeField] private Vector3 followOffset = new Vector3(0f, 14f, -13f);
    [SerializeField] private Vector3 followRotationEuler = new Vector3(48f, 0f, 0f);
    [SerializeField] private float followFOV = 52f;

    [Header("Vista General / Overview (Ver todos los vagones)")]
    [Tooltip("Posición base en Y y Z para la vista general cuando hay pocos vagones")]
    [SerializeField] private float overviewBaseHeightY = 24f;
    [SerializeField] private float overviewBaseDistanceZ = -22f;
    [Tooltip("Cuánto se aleja extra la cámara por cada vagón nuevo que acoples al tren")]
    [SerializeField] private float extraHeightPerWagon = 3.2f;
    [SerializeField] private float extraDistanceZPerWagon = 2.8f;
    [SerializeField] private Vector3 overviewRotationEuler = new Vector3(52f, 0f, 0f);
    [SerializeField] private float overviewFOV = 58f;

    [Header("Suavizado y Controles de Cambio de Vista")]
    [SerializeField] private float positionSmoothTime = 0.18f;
    [SerializeField] private float rotationLerpSpeed = 8f;
    [SerializeField] private KeyCode toggleViewKey = KeyCode.V;
    [SerializeField] private bool showOnScreenToggleButton = true;

    private bool isOverviewMode = false;
    private Vector3 currentVelocity;
    private Camera cam;
    private GUIStyle camButtonStyle;

    public bool IsOverviewMode => isOverviewMode;

    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        if (playerTarget == null)
        {
            PlayerMove pm = FindFirstObjectByType<PlayerMove>();
            if (pm != null) playerTarget = pm.transform;
            else
            {
                GameObject pObj = GameObject.FindGameObjectWithTag("Player");
                if (pObj != null) playerTarget = pObj.transform;
            }
        }
    }

    void Update()
    {
        // Permitir cambiar entre vista Player y vista Overview también con la tecla V (o Tab)
        if (Input.GetKeyDown(toggleViewKey) || Input.GetKeyDown(KeyCode.Tab))
        {
            ToggleCameraMode();
        }
    }

    void LateUpdate()
    {
        Vector3 targetPos;
        Quaternion targetRot;
        float targetFov;

        if (!isOverviewMode && playerTarget != null)
        {
            // MODO 1: Seguir de cerca al Player
            targetPos = playerTarget.position + followOffset;
            targetRot = Quaternion.Euler(followRotationEuler);
            targetFov = followFOV;
        }
        else
        {
            // MODO 2: Vista Overview que encuadra todos los vagones activos automáticamente
            GetTrainOverviewTarget(out targetPos, out targetRot, out targetFov);
        }

        transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, positionSmoothTime);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationLerpSpeed * Time.deltaTime);

        if (cam != null && !cam.orthographic)
        {
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, rotationLerpSpeed * Time.deltaTime);
        }
    }

    private void GetTrainOverviewTarget(out Vector3 targetPos, out Quaternion targetRot, out float targetFov)
    {
        float centerX = -5f;
        float centerZ = 0f;
        int wagonCount = 2;

        if (GameManager.Instance != null && GameManager.Instance.activeWagons.Count > 0)
        {
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            int validWagons = 0;

            foreach (WagonController w in GameManager.Instance.activeWagons)
            {
                if (w == null) continue;
                Bounds wb = w.GetWorldBounds();
                if (wb.min.x < minX) minX = wb.min.x;
                if (wb.max.x > maxX) maxX = wb.max.x;
                validWagons++;
            }

            if (validWagons > 0)
            {
                centerX = (minX + maxX) * 0.5f;
                wagonCount = validWagons;
            }
        }

        int extraWagons = Mathf.Max(0, wagonCount - 2);
        float heightY = overviewBaseHeightY + (extraWagons * extraHeightPerWagon);
        float distZ = overviewBaseDistanceZ - (extraWagons * extraDistanceZPerWagon);

        targetPos = new Vector3(centerX, heightY, centerZ + distZ);
        targetRot = Quaternion.Euler(overviewRotationEuler);
        targetFov = overviewFOV;
    }

    // Método público que puedes conectar a un botón UI de Unity (On Click) o usar con el botón en pantalla
    public void ToggleCameraMode()
    {
        isOverviewMode = !isOverviewMode;
        string modeText = isOverviewMode ? "VISTA GENERAL (Overview de Vagones)" : "SEGUIR AL PLAYER (Zoom Cercano)";
        Debug.Log($"[Cámara] Cambiada a: {modeText}");
    }

    public void SetOverviewMode(bool overview)
    {
        isOverviewMode = overview;
    }

    void OnGUI()
    {
        if (!showOnScreenToggleButton) return;
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        if (camButtonStyle == null)
        {
            camButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold
            };
            camButtonStyle.normal.textColor = Color.cyan;
            camButtonStyle.hover.textColor = Color.yellow;
        }

        string btnLabel = isOverviewMode
            ? "?? Vista Actual: OVERVIEW (Clic o [V] para Zoom al Player)"
            : "?? Vista Actual: PLAYER (Clic o [V] para ver Todos los Vagones)";

        // Botón grande en la esquina superior derecha para no estorbar al HUD de cartas
        float btnWidth = 540f;
        float btnHeight = 52f;
        Rect btnRect = new Rect(Screen.width - btnWidth - 20f, 20f, btnWidth, btnHeight);

        if (GUI.Button(btnRect, btnLabel, camButtonStyle))
        {
            ToggleCameraMode();
        }
    }
}