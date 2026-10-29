using System.Collections.Generic;
using UnityEngine;

public class CardDeckManager : MonoBehaviour
{
    [Header("Entrega Periódica de Cartas (Estilo Plants vs. Zombies)")]
    [SerializeField] private float cardDeliveryInterval = 6f;
    [SerializeField] private int maxCardsInHand = 4;
    public List<CardType> currentHand = new List<CardType>();

    [Header("Prefabs para Instanciar con las Cartas")]
    [SerializeField] private GameObject turret1AreaPrefab;
    [SerializeField] private GameObject turret2RapidPrefab;
    [SerializeField] private GameObject turret3HeavyPrefab;
    [SerializeField] private GameObject cargoWagonPrefab;
    [SerializeField] private float couplingGapX = 0.25f;
    [SerializeField] private int maxCargoWagons = 3;

    private int selectedHandIndex = -1;
    private float nextCardTimer = 0f;
    private Camera mainCam;

    // Estilos para el HUD al doble de tamaño
    private GUIStyle boxStyle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle buttonStyle;

    void Start()
    {
        mainCam = Camera.main;
        AddRandomCardToHand();
        AddRandomCardToHand();
    }

    void Update()
    {
        if (mainCam == null) mainCam = Camera.main;

        // Temporizador de cinta transportadora de cartas
        nextCardTimer += Time.deltaTime;
        if (nextCardTimer >= cardDeliveryInterval)
        {
            nextCardTimer = 0f;
            if (currentHand.Count < maxCardsInHand)
            {
                AddRandomCardToHand();
            }
        }

        // Selección de carta con teclas 1, 2, 3, 4
        if (Input.GetKeyDown(KeyCode.Alpha1)) SelectCard(0);
        if (Input.GetKeyDown(KeyCode.Alpha2)) SelectCard(1);
        if (Input.GetKeyDown(KeyCode.Alpha3)) SelectCard(2);
        if (Input.GetKeyDown(KeyCode.Alpha4)) SelectCard(3);

        // Clic derecho para cancelar selección
        if (Input.GetMouseButtonDown(1))
        {
            selectedHandIndex = -1;
        }

        // Ignorar clics dentro del área del HUD (880 x 250 px)
        bool mouseOverHud = Input.mousePosition.x < 880f && (Screen.height - Input.mousePosition.y) < 250f;

        // Clic izquierdo sobre un vagón para colocar la torreta seleccionada
        if (Input.GetMouseButtonDown(0) && !mouseOverHud && selectedHandIndex >= 0 && selectedHandIndex < currentHand.Count)
        {
            TryUseSelectedCard();
        }
    }

    private void AddRandomCardToHand()
    {
        CardType newCard = (CardType)Random.Range(0, 4);
        currentHand.Add(newCard);
    }

    public void SelectCard(int handIndex)
    {
        if (handIndex < 0 || handIndex >= currentHand.Count) return;

        // Si la carta elegida es NewWagon, acoplar el vagón inmediatamente justo detrás del último vagón sin desfase
        if (currentHand[handIndex] == CardType.NewWagon)
        {
            if (TryAttachNewWagon())
            {
                currentHand.RemoveAt(handIndex);
                selectedHandIndex = -1;
            }
            return;
        }

        selectedHandIndex = handIndex;
        Debug.Log($"[Mazo] Carta seleccionada [{handIndex + 1}]: {currentHand[handIndex]}. Haz clic en la mitad izquierda o derecha del techo de un vagón.");
    }

    private void TryUseSelectedCard()
    {
        CardType card = currentHand[selectedHandIndex];

        if (card == CardType.NewWagon)
        {
            if (TryAttachNewWagon())
            {
                currentHand.RemoveAt(selectedHandIndex);
                selectedHandIndex = -1;
            }
            return;
        }

        Ray ray = mainCam.ScreenPointToRay(Input.mousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 250f, ~0, QueryTriggerInteraction.Collide);

        // 1. Prioridad: si el clic tocó directamente una de las 2 bases de torreta (TurretSlotMarker)
        foreach (RaycastHit hit in hits)
        {
            TurretSlotMarker slotMarker = hit.collider.GetComponent<TurretSlotMarker>();
            if (slotMarker != null && slotMarker.ParentWagon != null && !slotMarker.ParentWagon.IsLocomotive)
            {
                GameObject prefabToPlace = GetPrefabForCard(card);
                if (prefabToPlace != null && slotMarker.ParentWagon.PlaceTurret(slotMarker.SlotIndex, prefabToPlace))
                {
                    currentHand.RemoveAt(selectedHandIndex);
                    selectedHandIndex = -1;
                    return;
                }
            }
        }

        // 2. Respaldo: si el clic tocó cualquier parte del cuerpo del vagón (PisoVagon), detectar si fue en la mitad izquierda (0) o derecha (1)
        foreach (RaycastHit hit in hits)
        {
            WagonController wagon = hit.collider.GetComponentInParent<WagonController>();
            if (wagon != null && !wagon.IsLocomotive)
            {
                int slotIdx = wagon.GetClosestSlotIndex(hit.point);
                GameObject prefabToPlace = GetPrefabForCard(card);
                if (prefabToPlace != null && wagon.PlaceTurret(slotIdx, prefabToPlace))
                {
                    currentHand.RemoveAt(selectedHandIndex);
                    selectedHandIndex = -1;
                    return;
                }
            }
        }
    }

    private GameObject GetPrefabForCard(CardType card)
    {
        switch (card)
        {
            case CardType.Turret1_Area: return turret1AreaPrefab;
            case CardType.Turret2_Rapid: return turret2RapidPrefab;
            case CardType.Turret3_Heavy: return turret3HeavyPrefab;
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

        int currentCargoCount = 0;
        WagonController leftmostWagon = null;
        float minX = float.MaxValue;

        foreach (var w in GameManager.Instance.activeWagons)
        {
            if (w == null) continue;
            if (!w.IsLocomotive) currentCargoCount++;

            Bounds wb = w.GetWorldBounds();
            if (wb.min.x < minX)
            {
                minX = wb.min.x;
                leftmostWagon = w;
            }
        }

        if (currentCargoCount >= maxCargoWagons || leftmostWagon == null)
        {
            Debug.Log("[Tren] Ya alcanzaste el límite máximo de vagones acoplados.");
            return false;
        }

        Bounds tailBounds = leftmostWagon.GetWorldBounds();
        GameObject newWagonObj = Instantiate(cargoWagonPrefab, leftmostWagon.transform.position, leftmostWagon.transform.rotation);
        newWagonObj.name = $"VagonCarga_{currentCargoCount + 1}";

        WagonController newWagon = newWagonObj.GetComponent<WagonController>();
        if (newWagon == null) newWagon = newWagonObj.AddComponent<WagonController>();

        // Calcular el borde derecho real del nuevo vagón y pegarlo exactamente detrás del borde izquierdo del último vagón
        Bounds newBounds = newWagon.GetWorldBounds();
        float desiredMaxX = tailBounds.min.x - couplingGapX;
        float shiftX = desiredMaxX - newBounds.max.x;
        float shiftY = tailBounds.center.y - newBounds.center.y;
        float shiftZ = tailBounds.center.z - newBounds.center.z;

        newWagonObj.transform.position += new Vector3(shiftX, shiftY, shiftZ);
        newWagon.CreateOrRefreshRoofSlots();

        GameManager.Instance.RegisterWagon(newWagon);
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
            fontSize = 19,
            fontStyle = FontStyle.Bold,
            wordWrap = false
        };
        labelStyle.normal.textColor = Color.white;

        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 18,
            fontStyle = FontStyle.Bold
        };
    }

    // HUD al doble de tamaño (880 x 240 px) sin texto cortado
    void OnGUI()
    {
        InitStyles();

        GUI.Box(new Rect(15, 15, 860, 235), "", boxStyle);
        GUI.Label(new Rect(25, 22, 840, 32), "TREN 2112 — HUD DE GREYBOX (Hito 2)", titleStyle);

        if (GameManager.Instance != null)
        {
            float progress = GameManager.Instance.GetRouteProgressNormalized() * 100f;
            string mode = GameManager.Instance.isWeaponOverchargeActive ? "SOBRECARGA TORRETAS" : "ACELERACIÓN";

            GUI.Label(new Rect(30, 60, 820, 28), $"Progreso a San Paula: {progress:F0}%   |   Modo [Q]: {mode}", labelStyle);
            GUI.Label(new Rect(30, 92, 820, 28), $"Sacrificar último vagón: [G]   |   Vagones activos: {GameManager.Instance.activeWagons.Count}", labelStyle);
        }

        GUI.Label(new Rect(30, 128, 820, 28), "Cartas en mano (Presiona 1-4 o haz clic en el botón, luego clic en el techo del vagón):", labelStyle);

        for (int i = 0; i < currentHand.Count; i++)
        {
            string cardLabel = currentHand[i] == CardType.NewWagon
                ? "Nuevo Vagón"
                : currentHand[i].ToString().Split('_')[0];

            string prefix = (i == selectedHandIndex) ? ">> [" : "[";
            string suffix = (i == selectedHandIndex) ? "] <<" : "]";

            if (GUI.Button(new Rect(30 + (i * 205), 165, 195, 65), $"{prefix}{i + 1}: {cardLabel}{suffix}", buttonStyle))
            {
                SelectCard(i);
            }
        }
    }
}