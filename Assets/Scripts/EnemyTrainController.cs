using UnityEngine;

public enum EnemyTrainPhase
{
    Inactive,
    Approaching,       // Alcanzándonos por las vías paralelas (Z = 10)
    CombatActive,      // Emparejado: barra de vida + barra de tiempo para destruirlo; zombies pausados
    BoardingWindow,    // Sin vida: 15s para saltar al tren enemigo, ir al drop junto a su locomotora y regresar
    ExplodingAndLeft,  // Destruido y abordado: explota y se queda atrás (-X)
    EscapingAhead      // No lo matamos a tiempo: acelera y se va hacia adelante (+X)
}

public class EnemyTrainController : MonoBehaviour
{
    public static EnemyTrainController Instance { get; private set; }

    [Header("Configuración de Aparición y Posición en Vías Paralelas")]
    [Tooltip("Porcentaje del recorrido (0.0 a 1.0) en el que aparece el tren enemigo (ej. 0.45 = 45%)")]
    [Range(0.1f, 0.9f)]
    [SerializeField] private float spawnAtRouteProgress = 0.45f;
    [Tooltip("Tecla rápida para forzar la aparición del tren enemigo en cualquier momento durante pruebas")]
    [SerializeField] private KeyCode debugForceSpawnKey = KeyCode.B;
    [SerializeField] private float parallelTrackZ = 10f;
    [SerializeField] private float startApproachX = -55f;
    [SerializeField] private float alignedCombatX = -5f;
    [SerializeField] private float escapeTargetX = 65f;
    [SerializeField] private float approachAndLeaveSpeed = 18f;

    [Header("Balance de Combate y Tiempos (Ajustables en Inspector)")]
    [SerializeField] private float baseEnemyTrainMaxHealth = 650f;
    [SerializeField] private float extraHealthPerZoneLevel = 200f;
    [Tooltip("Segundos que tienes para destruir al tren enemigo antes de que se vaya")]
    [SerializeField] private float combatTimeLimitSeconds = 30f;
    [Tooltip("Segundos que tienes tras matarlo para saltar, recoger el perk junto a la locomotora y regresar")]
    [SerializeField] private float boardingTimeLimitSeconds = 15f;
    [SerializeField] private float pickupRadius = 2.6f;

    public EnemyTrainPhase currentPhase = EnemyTrainPhase.Inactive;

    private float maxHealth;
    private float currentHealth;
    private float phaseTimer;
    private bool hasSpawnedThisLevel = false;
    private bool perkCollected = false;

    private GameObject trainRoot;
    private GameObject dropVisualObj;
    private Vector3 dropLocalOffset;
    private Bounds enemyTrainWalkableBounds;
    private RolledPerkInfo currentRolledPerk;
    private EnemyFSM targetProxyFSM;

    // Estilos de UI para las barras del Tren Enemigo y el Popup flotante del Drop
    private GUIStyle barBgStyle;
    private GUIStyle hpFillStyle;
    private GUIStyle timeFillStyle;
    private GUIStyle headerTextStyle;
    private GUIStyle popupBoxStyle;
    private GUIStyle boardButtonStyle;

    public bool IsPauseZombieSpawningActive =>
        currentPhase == EnemyTrainPhase.Approaching ||
        currentPhase == EnemyTrainPhase.CombatActive ||
        currentPhase == EnemyTrainPhase.BoardingWindow;

    void Awake()
    {
        Instance = this;
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        // Permitir spawnear el tren enemigo con la tecla B para probar rápido, o automáticamente al llegar al % de ruta
        if (!hasSpawnedThisLevel && currentPhase == EnemyTrainPhase.Inactive)
        {
            float progress = GameManager.Instance != null ? GameManager.Instance.GetRouteProgressNormalized() : 0f;
            if (progress >= spawnAtRouteProgress || Input.GetKeyDown(debugForceSpawnKey))
            {
                StartEnemyTrainEvent();
            }
        }

        switch (currentPhase)
        {
            case EnemyTrainPhase.Approaching:
                UpdateApproaching();
                break;
            case EnemyTrainPhase.CombatActive:
                UpdateCombatActive();
                break;
            case EnemyTrainPhase.BoardingWindow:
                UpdateBoardingWindow();
                break;
            case EnemyTrainPhase.ExplodingAndLeft:
                UpdateLeaving(-approachAndLeaveSpeed * 1.3f, startApproachX - 15f, finishLevelAfterLeave: perkCollected);
                break;
            case EnemyTrainPhase.EscapingAhead:
                UpdateLeaving(approachAndLeaveSpeed * 1.4f, escapeTargetX, finishLevelAfterLeave: false);
                break;
        }
    }

    public void StartEnemyTrainEvent()
    {
        if (currentPhase != EnemyTrainPhase.Inactive) return;

        hasSpawnedThisLevel = true;
        perkCollected = false;

        int zone = RunProgressionData.State.currentZoneLevel;
        maxHealth = baseEnemyTrainMaxHealth + (zone - 1) * extraHealthPerZoneLevel;
        currentHealth = maxHealth;

        // Sortear el Perk desde que aparece el tren para que la torreta elegida sea visible todo el tiempo en el popup
        currentRolledPerk = RunProgressionData.GenerateRandomPerk();

        BuildEnemyTrainVisualsAndDrop();

        currentPhase = EnemyTrainPhase.Approaching;
        Debug.Log($"[Tren Enemigo] ¡Apareció en las vías paralelas (Z={parallelTrackZ}) con el Drop: {currentRolledPerk.displayTitle}!");
    }

    private void BuildEnemyTrainVisualsAndDrop()
    {
        if (trainRoot != null) Destroy(trainRoot);

        trainRoot = new GameObject("EnemyTrain_Boss");
        trainRoot.transform.position = new Vector3(startApproachX, 0.9f, parallelTrackZ);

        // Construir Locomotora Enemiga (en X = +5.25 relativo al centro) y Segundo Vagón (en X = -5.25 relativo al centro)
        GameObject loco = CreateHollowWagonModel("Enemy_Locomotive", new Vector3(5.25f, 0f, 0f), new Color(0.45f, 0.12f, 0.12f, 1f));
        GameObject wagon2 = CreateHollowWagonModel("Enemy_SecondWagon", new Vector3(-5.25f, 0f, 0f), new Color(0.35f, 0.15f, 0.15f, 1f));

        // Agregar Collider general y un EnemyFSM en modo Boss para que TODAS las torretas y balas le disparen automáticamente
        BoxCollider bossCol = trainRoot.AddComponent<BoxCollider>();
        bossCol.center = new Vector3(0f, 2.5f, 0f);
        bossCol.size = new Vector3(20.5f, 5f, 5f);

        targetProxyFSM = trainRoot.AddComponent<EnemyFSM>();
        targetProxyFSM.ConfigureAsEnemyTrainTarget(this);

        // Crear el Drop flotando al final del segundo vagón, pegado a la locomotora (en X = -0.3 relativo a trainRoot)
        dropVisualObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
        dropVisualObj.name = "PerkDrop_FloatingCrystal";
        dropVisualObj.transform.SetParent(trainRoot.transform, false);
        dropLocalOffset = new Vector3(-0.3f, 1.35f, 0f);
        dropVisualObj.transform.localPosition = dropLocalOffset;
        dropVisualObj.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        dropVisualObj.transform.localRotation = Quaternion.Euler(35f, 45f, 35f);
        Destroy(dropVisualObj.GetComponent<Collider>());

        Renderer dropRend = dropVisualObj.GetComponent<Renderer>();
        if (dropRend != null)
        {
            dropRend.material.color = new Color(1f, 0.85f, 0.1f, 1f); // Dorado brillante visible desde que spawnea
        }
    }

    private GameObject CreateHollowWagonModel(string partName, Vector3 localOffset, Color bodyColor)
    {
        GameObject root = new GameObject(partName);
        root.transform.SetParent(trainRoot.transform, false);
        root.transform.localPosition = localOffset;

        // Piso (10 x 0.4 x 5)
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Piso";
        floor.transform.SetParent(root.transform, false);
        floor.transform.localPosition = new Vector3(0f, 0.2f, 0f);
        floor.transform.localScale = new Vector3(10f, 0.4f, 5f);
        floor.GetComponent<Renderer>().material.color = bodyColor;

        // 4 Pilares en las esquinas
        Vector3[] corners = new Vector3[]
        {
            new Vector3(-4.7f, 2.5f, -2.2f),
            new Vector3(-4.7f, 2.5f,  2.2f),
            new Vector3( 4.7f, 2.5f, -2.2f),
            new Vector3( 4.7f, 2.5f,  2.2f)
        };
        for (int i = 0; i < corners.Length; i++)
        {
            GameObject pillar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            pillar.name = $"Pilar_{i + 1}";
            pillar.transform.SetParent(root.transform, false);
            pillar.transform.localPosition = corners[i];
            pillar.transform.localScale = new Vector3(0.4f, 4.4f, 0.4f);
            pillar.GetComponent<Renderer>().material.color = bodyColor * 0.8f;
            Destroy(pillar.GetComponent<Collider>());
        }

        // Techo semi-transparente tipo vidrio entintado
        GameObject roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roof.name = "Techo_Vidrio";
        roof.transform.SetParent(root.transform, false);
        roof.transform.localPosition = new Vector3(0f, 4.8f, 0f);
        roof.transform.localScale = new Vector3(10f, 0.2f, 5f);
        Destroy(roof.GetComponent<Collider>());

        Renderer roofRend = roof.GetComponent<Renderer>();
        if (roofRend != null)
        {
            roofRend.material.color = new Color(0.15f, 0.2f, 0.25f, 0.35f);
        }

        return root;
    }

    private void UpdateApproaching()
    {
        if (trainRoot == null) return;

        RotateFloatingDrop();

        Vector3 pos = trainRoot.transform.position;
        pos.x = Mathf.MoveTowards(pos.x, alignedCombatX, approachAndLeaveSpeed * Time.deltaTime);
        trainRoot.transform.position = pos;

        if (Mathf.Abs(pos.x - alignedCombatX) <= 0.05f)
        {
            currentPhase = EnemyTrainPhase.CombatActive;
            phaseTimer = combatTimeLimitSeconds;
            Debug.Log("[Tren Enemigo] ¡Emparejado! Destrúyelo antes de que termine el tiempo.");
        }
    }

    private void UpdateCombatActive()
    {
        if (trainRoot == null) return;

        RotateFloatingDrop();
        phaseTimer -= Time.deltaTime;

        if (phaseTimer <= 0f)
        {
            // No lo matamos a tiempo: el tren enemigo se escapa hacia adelante
            currentPhase = EnemyTrainPhase.EscapingAhead;
            if (targetProxyFSM != null) Destroy(targetProxyFSM);
            Debug.Log("[Tren Enemigo] Se agotó el tiempo de combate. El tren enemigo escapa.");
        }
    }

    public void ReceiveDamageFromBullet(float amount)
    {
        if (currentPhase != EnemyTrainPhase.CombatActive) return;

        currentHealth = Mathf.Max(0f, currentHealth - amount);
        if (currentHealth <= 0f)
        {
            EnterBoardingPhase();
        }
    }

    private void EnterBoardingPhase()
    {
        currentPhase = EnemyTrainPhase.BoardingWindow;
        phaseTimer = boardingTimeLimitSeconds;

        // Desactivar el proxy EnemyFSM para que las torretas dejen de dispararle mientras el Player sube por el drop
        if (targetProxyFSM != null)
        {
            Destroy(targetProxyFSM);
        }

        UpdateEnemyWalkableBounds();
        PlayerMove pm = FindFirstObjectByType<PlayerMove>();
        if (pm != null)
        {
            pm.SetEnemyTrainBoardingWindow(true, enemyTrainWalkableBounds);
        }

        Debug.Log($"[Tren Enemigo] ¡Derrotado! Tienes {boardingTimeLimitSeconds}s para saltar a su vagón, recoger el Perk pegado a la locomotora y regresar.");
    }

    private void UpdateBoardingWindow()
    {
        if (trainRoot == null) return;

        RotateFloatingDrop();
        UpdateEnemyWalkableBounds();

        phaseTimer -= Time.deltaTime;

        PlayerMove pm = FindFirstObjectByType<PlayerMove>();
        if (pm != null)
        {
            // Verificar si el Player ya está en el tren enemigo y llegó hasta el Drop (final del 2do vagón, pegado a la locomotora)
            if (!perkCollected && dropVisualObj != null && pm.currentZone == TrainDeckZone.EnemyTrainBoarding)
            {
                float distToDrop = Vector2.Distance(
                    new Vector2(pm.transform.position.x, pm.transform.position.z),
                    new Vector2(dropVisualObj.transform.position.x, dropVisualObj.transform.position.z)
                );

                if (distToDrop <= pickupRadius)
                {
                    perkCollected = true;
                    RunProgressionData.ApplyCollectedPerk(currentRolledPerk);
                    dropVisualObj.SetActive(false);
                    Debug.Log($"[Tren Enemigo] ¡Recolectaste el Drop! Ahora regresa a la orilla inferior y salta de vuelta a tu tren.");
                }
            }

            // Si ya recogió el drop y ya regresó a nuestro tren, el tren enemigo explota inmediatamente y se queda atrás
            if (perkCollected && pm.currentZone == TrainDeckZone.PlayerTrain)
            {
                TriggerEnemyTrainExplosion();
                return;
            }
        }

        if (phaseTimer <= 0f)
        {
            // Si se acabó el tiempo de abordaje, regresar al Player de emergencia a su tren y explotar el tren enemigo
            if (pm != null)
            {
                pm.SetEnemyTrainBoardingWindow(false, enemyTrainWalkableBounds);
            }
            TriggerEnemyTrainExplosion();
        }
    }

    private void TriggerEnemyTrainExplosion()
    {
        PlayerMove pm = FindFirstObjectByType<PlayerMove>();
        if (pm != null)
        {
            pm.SetEnemyTrainBoardingWindow(false, enemyTrainWalkableBounds);
        }

        // Pintar el tren enemigo de negro/quemado para simular la explosión en greybox y dejarlo atrás
        if (trainRoot != null)
        {
            foreach (Renderer r in trainRoot.GetComponentsInChildren<Renderer>())
            {
                r.material.color = new Color(0.12f, 0.12f, 0.12f, 1f);
            }
        }

        currentPhase = EnemyTrainPhase.ExplodingAndLeft;
        Debug.Log("[Tren Enemigo] ¡El tren enemigo explotó y se queda atrás!");
    }

    private void UpdateLeaving(float speedX, float targetLimitX, bool finishLevelAfterLeave)
    {
        if (trainRoot == null) return;

        trainRoot.transform.Translate(Vector3.right * speedX * Time.deltaTime, Space.World);

        bool reachedEnd = (speedX < 0f && trainRoot.transform.position.x <= targetLimitX) ||
                          (speedX > 0f && trainRoot.transform.position.x >= targetLimitX);

        if (reachedEnd)
        {
            Destroy(trainRoot);
            currentPhase = EnemyTrainPhase.Inactive;

            // Si recolectamos el drop del tren, finalizar el nivel actual y pasar a la escena intermedia de Resumen/Perks
            if (finishLevelAfterLeave && GameManager.Instance != null)
            {
                GameManager.Instance.CompleteZoneAndOpenSummary();
            }
        }
    }

    private void UpdateEnemyWalkableBounds()
    {
        if (trainRoot != null)
        {
            // Superficie interior caminable de los 2 vagones del tren enemigo en Z = 10
            enemyTrainWalkableBounds = new Bounds(
                new Vector3(trainRoot.transform.position.x, trainRoot.transform.position.y + 0.2f, parallelTrackZ),
                new Vector3(20f, 0.4f, 4.6f)
            );
        }
    }

    private void RotateFloatingDrop()
    {
        if (dropVisualObj != null && dropVisualObj.activeSelf)
        {
            dropVisualObj.transform.Rotate(Vector3.up * 90f * Time.deltaTime, Space.World);
            float bobY = dropLocalOffset.y + Mathf.Sin(Time.time * 3.5f) * 0.25f;
            dropVisualObj.transform.localPosition = new Vector3(dropLocalOffset.x, bobY, dropLocalOffset.z);
        }
    }

    private void InitStyles()
    {
        if (barBgStyle != null) return;

        barBgStyle = new GUIStyle(GUI.skin.box);

        Texture2D redTex = new Texture2D(1, 1);
        redTex.SetPixel(0, 0, new Color(0.85f, 0.18f, 0.18f, 0.95f));
        redTex.Apply();
        hpFillStyle = new GUIStyle(GUI.skin.box);
        hpFillStyle.normal.background = redTex;

        Texture2D amberTex = new Texture2D(1, 1);
        amberTex.SetPixel(0, 0, new Color(0.95f, 0.65f, 0.12f, 0.95f));
        amberTex.Apply();
        timeFillStyle = new GUIStyle(GUI.skin.box);
        timeFillStyle.normal.background = amberTex;

        headerTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        headerTextStyle.normal.textColor = Color.white;

        popupBoxStyle = new GUIStyle(GUI.skin.box)
        {
            fontSize = 15,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        popupBoxStyle.normal.textColor = Color.yellow;

        boardButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };
        boardButtonStyle.normal.textColor = Color.yellow;
    }

    void OnGUI()
    {
        if (currentPhase == EnemyTrainPhase.Inactive) return;
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        InitStyles();

        // 1. Dibujar el Popup flotante sobre el Drop todo el tiempo (mostrando desde el inicio qué torreta/perk dará)
        if (dropVisualObj != null && dropVisualObj.activeSelf && Camera.main != null && currentRolledPerk != null)
        {
            Vector3 worldPopupPos = dropVisualObj.transform.position + Vector3.up * 2.2f;
            Vector3 screenPos = Camera.main.WorldToScreenPoint(worldPopupPos);
            if (screenPos.z > 0f)
            {
                float guiX = screenPos.x - 150f;
                float guiY = Screen.height - screenPos.y - 60f;
                string stateHint = (currentPhase == EnemyTrainPhase.BoardingWindow)
                    ? "¡LISTO PARA RECOGER!"
                    : "(Destruye el tren para abordar)";

                GUI.Box(new Rect(guiX, guiY, 300f, 64f), $"🎁 DROP: {currentRolledPerk.displayPopupShort} { stateHint} ", popupBoxStyle);
            }
        }

        // 2. Dibujar las 2 Barras del Tren Enemigo (Barra de Vida y Barra de Tiempo) en la parte superior central
        if (currentPhase == EnemyTrainPhase.CombatActive || currentPhase == EnemyTrainPhase.BoardingWindow)
        {
            float boxW = 540f;
            float boxH = 115f;
            float boxX = (Screen.width - boxW) * 0.5f;
            float boxY = 85f;

            GUI.Box(new Rect(boxX, boxY, boxW, boxH), "", barBgStyle);

            if (currentPhase == EnemyTrainPhase.CombatActive)
            {
                float hpPct = Mathf.Clamp01(currentHealth / Mathf.Max(1f, maxHealth));
                float timePct = Mathf.Clamp01(phaseTimer / Mathf.Max(1f, combatTimeLimitSeconds));

                GUI.Label(new Rect(boxX, boxY + 6f, boxW, 24f), "🚂 TREN ENEMIGO EMPAREJADO — ¡DESTRÚYELO!", headerTextStyle);

                // Barra de Vida
                GUI.Box(new Rect(boxX + 20f, boxY + 36f, boxW - 40f, 28f), "");
                GUI.Box(new Rect(boxX + 20f, boxY + 36f, (boxW - 40f) * hpPct, 28f), "", hpFillStyle);
                GUI.Label(new Rect(boxX + 20f, boxY + 38f, boxW - 40f, 24f), $"Vida del Tren Enemigo: {currentHealth:F0} / {maxHealth:F0}", headerTextStyle);

                // Barra de Tiempo
                GUI.Box(new Rect(boxX + 20f, boxY + 72f, boxW - 40f, 28f), "");
                GUI.Box(new Rect(boxX + 20f, boxY + 72f, (boxW - 40f) * timePct, 28f), "", timeFillStyle);
                GUI.Label(new Rect(boxX + 20f, boxY + 74f, boxW - 40f, 24f), $"Tiempo antes de que escape: {phaseTimer:F1} s", headerTextStyle);
            }
            else if (currentPhase == EnemyTrainPhase.BoardingWindow)
            {
                float timePct = Mathf.Clamp01(phaseTimer / Mathf.Max(1f, boardingTimeLimitSeconds));
                string titleMsg = perkCollected
                    ? "¡PERK RECOLECTADO! Regresa a la orilla inferior y salta a tu tren"
                    : "¡TREN INMOVILIZADO! Salta al otro tren y corre hacia su locomotora por el Drop";

                GUI.Label(new Rect(boxX, boxY + 8f, boxW, 26f), titleMsg, headerTextStyle);

                GUI.Box(new Rect(boxX + 20f, boxY + 42f, boxW - 40f, 32f), "");
                GUI.Box(new Rect(boxX + 20f, boxY + 42f, (boxW - 40f) * timePct, 32f), "", timeFillStyle);
                GUI.Label(new Rect(boxX + 20f, boxY + 46f, boxW - 40f, 24f), $"Tiempo antes de que explote: {phaseTimer:F1} s", headerTextStyle);

                // Botón en el HUD para saltar al tren enemigo (o regresar) cuando el jugador está cerca de la orilla
                PlayerMove pm = FindFirstObjectByType<PlayerMove>();
                if (pm != null && pm.IsNearBoardingEdge(out string actionLabel))
                {
                    if (GUI.Button(new Rect(boxX + 60f, boxY + 125f, boxW - 120f, 55f), $"{actionLabel} [ESPACIO o CLIC AQUÍ]", boardButtonStyle))
                    {
                        pm.TriggerBoardingJumpFromUI();
                    }
                }
            }
        }
    }
}