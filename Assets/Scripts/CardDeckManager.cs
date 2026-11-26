using System.Collections.Generic;
using UnityEngine;

public class CardDeckManager : MonoBehaviour
{
    [Header("Mano actual (tiempos y probabilidades: ver BALANCE en GameManager)")]
    public List<EnumCardType> currentHand = new List<EnumCardType>();

    [Header("Prefabs para Instanciar con las Cartas")]
    [SerializeField] private GameObject turret1AreaPrefab;
    [SerializeField] private GameObject turret2RapidPrefab;
    [SerializeField] private GameObject turret3HeavyPrefab;
    [SerializeField] private GameObject turret4LanzallamasPrefab;
    [SerializeField] private GameObject turret5WaterHosePrefab;
    [SerializeField] private GameObject cargoWagonPrefab;

    [Header("Ajustes de Spawneo de Vagones")]
    [SerializeField] private float couplingGapX = 0.25f;
    [Tooltip("Posición Z exacta donde se colocará cada vagón nuevo (0 por defecto)")]
    [SerializeField] private float targetWagonZ = 0f;

    [Header("Debug de pruebas")]
    [SerializeField] private bool enableDebugKeys = true;

    private int selectedHandIndex = -1;
    private float turretCardTimer = 0f;
    private float wagonCardTimer = 0f;
    private Camera mainCam;
    private int lastGuiButtonClickFrame = -1;

    // Balance values come from GameManager (fallbacks if it is missing)
    int MaxCargoWagons => GameManager.Instance != null ? GameManager.Instance.maxCargoWagons : 10;
    int MaxCardsInHand => GameManager.Instance != null ? GameManager.Instance.maxCardsInHand : 4;
    float TurretCardInterval => GameManager.Instance != null ? GameManager.Instance.turretCardInterval : 6f;
    float WagonCardInterval => GameManager.Instance != null ? GameManager.Instance.wagonCardInterval : 15f;

    private readonly EnumCardType[] allTurretCards = new EnumCardType[]
    {
        EnumCardType.Turret1_Area,
        EnumCardType.Turret2_Rapid,
        EnumCardType.Turret3_Heavy,
        EnumCardType.Turret4_Lanzallamas,
        EnumCardType.Turret5_WaterHose
    };

    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle controlsStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;

    void Start()
    {
        mainCam = Camera.main;

        int starting = GameManager.Instance != null ? GameManager.Instance.startingCards : 2;
        bool onlyTurrets = GameManager.Instance != null && GameManager.Instance.startingHandOnlyTurrets;

        for (int i = 0; i < starting; i++)
        {
            if (onlyTurrets) AddRandomTurretCard();
            else AddRandomCardToHand();
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;
        if (mainCam == null) mainCam = Camera.main;

        UpdateCardTimers();

        if (enableDebugKeys)
        {
            if (Input.GetKeyDown(KeyCode.F1)) currentHand.Add(EnumCardType.NewWagon);
            if (Input.GetKeyDown(KeyCode.F2)) AddRandomTurretCard();
            if (Input.GetKeyDown(KeyCode.F3)) { currentHand.Clear(); selectedHandIndex = -1; }
        }

        if (Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1)) SelectCard(0);
        if (Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2)) SelectCard(1);
        if (Input.GetKeyDown(KeyCode.Alpha3) || Input.GetKeyDown(KeyCode.Keypad3)) SelectCard(2);
        if (Input.GetKeyDown(KeyCode.Alpha4) || Input.GetKeyDown(KeyCode.Keypad4)) SelectCard(3);

        if (Input.GetMouseButtonDown(1))
        {
            selectedHandIndex = -1;
        }

        bool mouseOverHud = Input.mousePosition.x < 900f && (Screen.height - Input.mousePosition.y) < 270f;
        bool mouseOverCamBtn = Input.mousePosition.x > (Screen.width - 570f) && (Screen.height - Input.mousePosition.y) < 85f;

        if (Input.GetMouseButtonDown(0) && !mouseOverHud && !mouseOverCamBtn && Time.frameCount > lastGuiButtonClickFrame)
        {
            if (selectedHandIndex >= 0 && selectedHandIndex < currentHand.Count)
            {
                TryUseSelectedCard();
            }
        }
    }

    // Two independent conveyor belts. If the hand is full the timer waits,
    // so the card arrives as soon as a slot frees up.
    private void UpdateCardTimers()
    {
        turretCardTimer += Time.deltaTime;
        wagonCardTimer += Time.deltaTime;

        if (turretCardTimer >= TurretCardInterval && currentHand.Count < MaxCardsInHand)
        {
            turretCardTimer = 0f;
            AddRandomTurretCard();
        }

        if (wagonCardTimer >= WagonCardInterval && currentHand.Count < MaxCardsInHand
            && GetCurrentCargoWagonCount() < MaxCargoWagons)
        {
            wagonCardTimer = 0f;
            currentHand.Add(EnumCardType.NewWagon);
        }
    }

    private int GetCurrentCargoWagonCount()
    {
        if (GameManager.Instance == null) return 0;
        int count = 0;
        foreach (var w in GameManager.Instance.activeWagons)
        {
            if (w != null && !w.IsLocomotive) count++;
        }
        return count;
    }

    // Starting hand: mix of wagon and turret cards (faster belt = more likely)
    private void AddRandomCardToHand()
    {
        bool canAddMoreWagons = GetCurrentCargoWagonCount() < MaxCargoWagons;
        float wagonRate = 1f / Mathf.Max(0.1f, WagonCardInterval);
        float turretRate = 1f / Mathf.Max(0.1f, TurretCardInterval);
        float wagonChance = wagonRate / (wagonRate + turretRate);

        if (canAddMoreWagons && Random.value < wagonChance)
        {
            currentHand.Add(EnumCardType.NewWagon);
            return;
        }
        AddRandomTurretCard();
    }

    private void AddRandomTurretCard()
    {
        float total = 0f;
        if (GameManager.Instance != null)
        {
            foreach (EnumCardType t in allTurretCards)
                total += GameManager.Instance.GetTuning(t).cardWeight;
        }

        EnumCardType picked;
        if (total <= 0f)
        {
            picked = allTurretCards[Random.Range(0, allTurretCards.Length)];
        }
        else
        {
            float roll = Random.value * total;
            picked = allTurretCards[allTurretCards.Length - 1];
            foreach (EnumCardType t in allTurretCards)
            {
                roll -= GameManager.Instance.GetTuning(t).cardWeight;
                if (roll <= 0f) { picked = t; break; }
            }
        }
        currentHand.Add(picked);
    }

    public void SelectCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= currentHand.Count) return;

        selectedHandIndex = handIndex;
        EnumCardType chosen = currentHand[handIndex];

        if (chosen == EnumCardType.NewWagon)
        {
            if (TryAttachNewWagon())
            {
                RemoveCardAt(handIndex);
            }
            return;
        }

        Debug.Log($"[Mazo] Carta seleccionada [{handIndex + 1}]: {chosen}. Haz clic sobre el techo de un vagón para colocarla.");
    }

    private void RemoveCardAt(int index)
    {
        if (index < 0 || index >= currentHand.Count) return;
        currentHand.RemoveAt(index);
        selectedHandIndex = -1;
    }

    private void TryUseSelectedCard()
    {
        if (selectedHandIndex < 0 || selectedHandIndex >= currentHand.Count) return;

        EnumCardType card = currentHand[selectedHandIndex];

        if (card == EnumCardType.NewWagon)
        {
            SelectCard(selectedHandIndex);
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 300f, ~0, QueryTriggerInteraction.Collide);

        foreach (RaycastHit hit in hits)
        {
            TurretSlotMarker slotMarker = hit.collider.GetComponent<TurretSlotMarker>();
            if (slotMarker != null && slotMarker.ParentWagon != null && !slotMarker.ParentWagon.IsLocomotive)
            {
                GameObject prefabToPlace = GetPrefabForCard(card);
                if (prefabToPlace != null && slotMarker.ParentWagon.PlaceTurret(slotMarker.SlotIndex, prefabToPlace))
                {
                    RemoveCardAt(selectedHandIndex);
                    return;
                }
            }
        }

        foreach (RaycastHit hit in hits)
        {
            WagonController wagon = hit.collider.GetComponentInParent<WagonController>();
            if (wagon != null && !wagon.IsLocomotive)
            {
                int slotIdx = wagon.GetClosestSlotIndex(hit.point);
                GameObject prefabToPlace = GetPrefabForCard(card);
                if (prefabToPlace != null && wagon.PlaceTurret(slotIdx, prefabToPlace))
                {
                    RemoveCardAt(selectedHandIndex);
                    return;
                }
            }
        }
    }

    private GameObject GetPrefabForCard(EnumCardType card)
    {
        switch (card)
        {
            case EnumCardType.Turret1_Area: return turret1AreaPrefab;
            case EnumCardType.Turret2_Rapid: return turret2RapidPrefab;
            case EnumCardType.Turret3_Heavy: return turret3HeavyPrefab;
            case EnumCardType.Turret4_Lanzallamas: return turret4LanzallamasPrefab;
            case EnumCardType.Turret5_WaterHose: return turret5WaterHosePrefab;
            default: return null;
        }
    }

    private bool TryAttachNewWagon()
    {
        if (GameManager.Instance == null || cargoWagonPrefab == null)
        {
            Debug.LogWarning("[Tren] Asigna 'Cargo Wagon Prefab' en el Inspector de CardDeckManager.");
            return false;
        }

        WagonController[] allSceneWagons = FindObjectsByType<WagonController>(FindObjectsSortMode.None);
        foreach (WagonController sw in allSceneWagons)
        {
            if (sw != null)
            {
                GameManager.Instance.RegisterWagon(sw);
            }
        }

        int currentCargoCount = 0;
        WagonController leftmostWagon = null;
        float minCenterX = float.MaxValue;

        foreach (WagonController w in allSceneWagons)
        {
            if (w == null) continue;
            if (!w.IsLocomotive) currentCargoCount++;

            Bounds wb = w.GetWorldBounds();
            if (wb.center.x < minCenterX)
            {
                minCenterX = wb.center.x;
                leftmostWagon = w;
            }
        }

        if (leftmostWagon == null)
        {
            Debug.LogWarning("[Tren] No se encontró ningún vagón base en la escena.");
            return false;
        }

        if (currentCargoCount >= MaxCargoWagons)
        {
            Debug.Log($"[Tren] Límite máximo de vagones alcanzado ({currentCargoCount}/{MaxCargoWagons}).");
            return false;
        }

        Bounds tailBounds = leftmostWagon.GetWorldBounds();
        float wagonLengthX = Mathf.Max(8f, tailBounds.size.x);
        float stepDistanceX = wagonLengthX + couplingGapX;

        Vector3 spawnPos = new Vector3(
            leftmostWagon.transform.position.x - stepDistanceX,
            leftmostWagon.transform.position.y,
            targetWagonZ
        );

        GameObject newWagonObj = Instantiate(cargoWagonPrefab, spawnPos, leftmostWagon.transform.rotation);
        newWagonObj.name = $"{cargoWagonPrefab.name} ({currentCargoCount + 1})";

        WagonController newWagon = newWagonObj.GetComponent<WagonController>();
        if (newWagon == null) newWagon = newWagonObj.AddComponent<WagonController>();

        newWagon.SetAsCargoWagon();
        newWagon.AlignChildMeshToZ(targetWagonZ);
        newWagon.CreateOrRefreshRoofSlots();

        GameManager.Instance.RegisterWagon(newWagon);

        Debug.Log($"[Tren] Vagón #{currentCargoCount + 1} '{newWagonObj.name}' creado detrás de '{leftmostWagon.name}' en Posición: {newWagonObj.transform.position}");
        return true;
    }

    private void InitStyles()
    {
        if (boxStyle != null) return;

        boxStyle = new GUIStyle(GUI.skin.box);
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleCenter
        };
        titleStyle.normal.textColor = Color.white;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            wordWrap = false
        };
        labelStyle.normal.textColor = Color.white;

        controlsStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Normal,
            wordWrap = false
        };
        controlsStyle.normal.textColor = new Color(0.85f, 0.92f, 1f, 1f);

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
        buttonStyle.normal.textColor = Color.white;

        selectedButtonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 19,
            fontStyle = FontStyle.Bold
        };
        selectedButtonStyle.normal.textColor = Color.yellow;
        selectedButtonStyle.hover.textColor = Color.yellow;
    }

    void OnGUI()
    {
        InitStyles();
        GUIUtility.keyboardControl = 0;

        Event e = Event.current;
        if (e != null && e.type == EventType.KeyDown && (GameManager.Instance == null || !GameManager.Instance.isPaused))
        {
            if (e.keyCode == KeyCode.Alpha1 || e.keyCode == KeyCode.Keypad1) { SelectCard(0); e.Use(); }
            else if (e.keyCode == KeyCode.Alpha2 || e.keyCode == KeyCode.Keypad2) { SelectCard(1); e.Use(); }
            else if (e.keyCode == KeyCode.Alpha3 || e.keyCode == KeyCode.Keypad3) { SelectCard(2); e.Use(); }
            else if (e.keyCode == KeyCode.Alpha4 || e.keyCode == KeyCode.Keypad4) { SelectCard(3); e.Use(); }
        }

        GUI.Box(new Rect(15, 15, 900, 260), "", boxStyle);
        GUI.Label(new Rect(25, 20, 880, 30), "TREN 2112 — HUD DE GREYBOX (Hito 2 · First Playable)", titleStyle);

        if (GameManager.Instance != null)
        {
            float progress = GameManager.Instance.GetRouteProgressNormalized() * 100f;
            string mode = GameManager.Instance.isWeaponOverchargeActive ? "SOBRECARGA TORRETAS" : "ACELERACIÓN";

            GUI.Label(new Rect(30, 54, 860, 26), $"Progreso a San Paula: {progress:F0}%   |   Modo [Q]: {mode}   |   Cámara [V]: Cambiar Vista", labelStyle);
            GUI.Label(new Rect(30, 82, 860, 26), $"Sacrificar último vagón: [G]   |   Vagones de carga: {GetCurrentCargoWagonCount()}/{MaxCargoWagons}   |   Pausa: [ESC]", labelStyle);
        }

        string debugHint = enableDebugKeys ? "   |   DEBUG: [F1] +Vagón  [F2] +Torreta  [F3] Vaciar" : "";
        GUI.Label(new Rect(30, 112, 860, 24), "CONTROLES: [WASD] Moverse  |  [Click Izq] Disparar  |  [X] Recargar  |  [V / Botón Der.] Vista General" + debugHint, controlsStyle);
        GUI.Label(new Rect(30, 140, 860, 26), "Cartas en mano (Presiona [1-4] o haz clic en el botón, luego clic en el techo del vagón):", labelStyle);

        for (int i = 0; i < currentHand.Count; i++)
        {
            string cardLabel = currentHand[i] == EnumCardType.NewWagon
                ? "Nuevo Vagón"
                : currentHand[i].ToString().Split('_')[0];

            bool isSelected = (i == selectedHandIndex);
            string prefix = isSelected ? ">> [" : "[";
            string suffix = isSelected ? "] <<" : "]";
            GUIStyle styleToUse = isSelected ? selectedButtonStyle : buttonStyle;

            if (GUI.Button(new Rect(30 + (i * 210), 174, 200, 62), $"{prefix}{i + 1}: {cardLabel}{suffix}", styleToUse))
            {
                lastGuiButtonClickFrame = Time.frameCount;
                SelectCard(i);
            }
        }

        if (GameManager.Instance != null && GameManager.Instance.isPaused)
        {
            float pw = 460f;
            float ph = 280f;
            float px = (Screen.width - pw) * 0.5f;
            float py = (Screen.height - ph) * 0.5f;

            GUI.Box(new Rect(px, py, pw, ph), "", boxStyle);
            GUI.Label(new Rect(px + 20, py + 20, pw - 40, 35), "JUEGO EN PAUSA", titleStyle);

            if (GUI.Button(new Rect(px + 60, py + 75, pw - 120, 50), "Continuar Partida [ESC]", buttonStyle))
            {
                GameManager.Instance.TogglePause();
            }
            if (GUI.Button(new Rect(px + 60, py + 138, pw - 120, 50), "Reiniciar Nivel", buttonStyle))
            {
                GameManager.Instance.LoadGameScene();
            }
            if (GUI.Button(new Rect(px + 60, py + 201, pw - 120, 50), "Volver al Menú Principal", buttonStyle))
            {
                GameManager.Instance.LoadStartMenu();
            }
        }
    }
}