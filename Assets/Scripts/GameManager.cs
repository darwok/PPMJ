using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[System.Serializable]
public class TurretTuning
{
    [Min(0.1f)] public float fireRateMultiplier = 1f;
    [Min(0.1f)] public float damageMultiplier = 1f;
    [Min(0.1f)] public float rangeMultiplier = 1f;
    [Tooltip("Relative chance of this turret card appearing (0 = never)")]
    [Min(0f)] public float cardWeight = 1f;
}

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencias de Sistemas")]
    public CardDeckManager cardDeckManager;
    [Tooltip("Opcional (en StartMenu): arrastra aquí el botón de 'Continuar Partida' para oscurecerlo/desactivarlo si no hay datos guardados")]
    public Button continueSavedGameButton;

    [Header("Lista de Vagones Activos (Ordenados de Punta a Cola)")]
    public List<WagonController> activeWagons = new List<WagonController>();
    public Transform currentTarget;

    // ================= BALANCE (edit these in the Inspector, even in Play Mode) =================

    [Header("BALANCE - Vagones")]
    [Min(1)] public int maxCargoWagons = 10;
    [Min(1)] public float wagonMaxHealth = 150f;
    [Min(1)] public float locomotiveMaxHealth = 150f;

    [Header("BALANCE - Cartas")]
    [Tooltip("Segundos entre cartas de TORRETA")]
    [Min(0.5f)] public float turretCardInterval = 6f;
    [Tooltip("Segundos entre cartas de NUEVO VAGÓN (pon un número enorme para desactivar)")]
    [Min(0.5f)] public float wagonCardInterval = 15f;
    [Range(1, 4)] public int maxCardsInHand = 4;
    [Range(0, 4)] public int startingCards = 2;
    public bool startingHandOnlyTurrets = false;

    [Header("BALANCE - Torretas (globales)")]
    [Min(0.1f)] public float globalFireRateMultiplier = 1f;
    [Min(0.1f)] public float globalDamageMultiplier = 1f;
    [Min(0.1f)] public float globalRangeMultiplier = 1f;
    [SerializeField] private float turretFireRateMultiplierWhenOvercharged = 1.5f;

    [Header("BALANCE - Torretas (por tipo)")]
    public TurretTuning turret1Area = new TurretTuning();
    public TurretTuning turret2Rapid = new TurretTuning();
    public TurretTuning turret3Heavy = new TurretTuning();
    public TurretTuning turret4Flamethrower = new TurretTuning();
    public TurretTuning turret5WaterHose = new TurretTuning();

    [Header("BALANCE - Ruta / Velocidad")]
    [SerializeField] private float routeDurationSeconds = 180f;
    [SerializeField] private float speedMultiplierWhenAccelerating = 1.35f;
    [SerializeField] private float speedMultiplierWhenOvercharged = 0.85f;

    // ===========================================================================================

    private float currentRouteTime = 0f;
    private bool gameEnded = false;
    private bool showingInlineLevelSummary = false;
    public bool isPaused = false;

    [Header("Modo de energía (Tecla Q)")]
    public bool isWeaponOverchargeActive = false;

    [Header("Bonos Adicionales en Partida")]
    public float globalAttackSpeedBonus = 0f;
    public float globalAttackRangeBonus = 0f;

    [Header("Nombres de Escenas")]
    [SerializeField] private string startMenuScene = "StartMenu";
    [SerializeField] private string gameScene = "GameScene";
    [SerializeField] private string levelSummaryScene = "LevelSummary";
    [SerializeField] private string victoryScene = "Victory";
    [SerializeField] private string gameOverScene = "GameOver";

    // ---------- Balance helpers used by other scripts ----------

    public TurretTuning GetTuning(EnumCardType type)
    {
        switch (type)
        {
            case EnumCardType.Turret1_Area: return turret1Area;
            case EnumCardType.Turret2_Rapid: return turret2Rapid;
            case EnumCardType.Turret3_Heavy: return turret3Heavy;
            case EnumCardType.Turret4_Lanzallamas: return turret4Flamethrower;
            case EnumCardType.Turret5_WaterHose: return turret5WaterHose;
            default: return turret2Rapid;
        }
    }

    // Final multipliers for a given turret type (global x per-type)
    public float GetFireRateFor(EnumCardType type)
    {
        return GetTurretFireRateMultiplier() * globalFireRateMultiplier * GetTuning(type).fireRateMultiplier;
    }

    public float GetDamageFor(EnumCardType type)
    {
        return globalDamageMultiplier * GetTuning(type).damageMultiplier;
    }

    public float GetRangeFor(EnumCardType type)
    {
        return globalRangeMultiplier * GetTuning(type).rangeMultiplier;
    }

    public int CargoWagonCount
    {
        get
        {
            int count = 0;
            foreach (WagonController w in activeWagons)
                if (w != null && !w.IsLocomotive) count++;
            return count;
        }
    }

    public bool CanAddCargoWagon()
    {
        return CargoWagonCount < maxCargoWagons;
    }

    // ---------- Unity ----------

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        Time.timeScale = 1f;

        if (cardDeckManager == null)
        {
            cardDeckManager = GetComponent<CardDeckManager>();
        }
    }

    void Start()
    {
        if (cardDeckManager == null)
        {
            cardDeckManager = FindFirstObjectByType<CardDeckManager>();
        }

        RefreshContinueButtonState();

        if (activeWagons.Count == 0)
        {
            activeWagons.AddRange(FindObjectsByType<WagonController>(FindObjectsSortMode.InstanceID));
        }
        UpdatePrimaryTarget();
    }

    public void RefreshContinueButtonState()
    {
        if (continueSavedGameButton != null)
        {
            bool hasSave = RunProgressionData.HasSavedRun();
            continueSavedGameButton.interactable = hasSave;

            ColorBlock cb = continueSavedGameButton.colors;
            cb.disabledColor = new Color(0.25f, 0.25f, 0.25f, 0.55f);
            continueSavedGameButton.colors = cb;
        }
    }

    void Update()
    {
        if (gameEnded || showingInlineLevelSummary) return;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            TogglePause();
        }

        if (isPaused) return;

        if (Input.GetKeyDown(KeyCode.Q))
        {
            isWeaponOverchargeActive = !isWeaponOverchargeActive;
            string modeName = isWeaponOverchargeActive ? "SOBRECARGA DE TORRETAS" : "ACELERACIÓN DE LOCOMOTORA";
            Debug.Log($"[Locomotora] Modo de energía cambiado a: {modeName}");
        }

        if (Input.GetKeyDown(KeyCode.G))
        {
            DetachLastCargoWagon();
        }

        float speedFactor = isWeaponOverchargeActive ? 1f : speedMultiplierWhenAccelerating;
        currentRouteTime += Time.deltaTime * speedFactor;

        if (currentRouteTime >= routeDurationSeconds)
        {
            CompleteZoneAndOpenSummary();
        }
    }

    public void TogglePause()
    {
        isPaused = !isPaused;
        Time.timeScale = isPaused ? 0f : 1f;
    }

    public float GetRouteProgressNormalized()
    {
        return Mathf.Clamp01(currentRouteTime / routeDurationSeconds);
    }

    public float GetTurretFireRateMultiplier()
    {
        float baseMult = isWeaponOverchargeActive ? turretFireRateMultiplierWhenOvercharged : 1f;
        return baseMult + globalAttackSpeedBonus;
    }

    public float GetTrainScrollSpeedMultiplier()
    {
        return isWeaponOverchargeActive ? speedMultiplierWhenOvercharged : speedMultiplierWhenAccelerating;
    }

    public void ApplyGlobalTrainPerk(float extraAttackSpeed, float extraAttackRange)
    {
        globalAttackSpeedBonus += extraAttackSpeed;
        globalAttackRangeBonus += extraAttackRange;
    }

    public void RegisterWagon(WagonController wagon)
    {
        if (!activeWagons.Contains(wagon))
        {
            activeWagons.Add(wagon);
            UpdatePrimaryTarget();
        }
    }

    public void OnWagonRemoved(WagonController removedWagon, bool wasLocomotive)
    {
        if (activeWagons.Contains(removedWagon))
        {
            activeWagons.Remove(removedWagon);
        }

        int remainingCargoWagons = CargoWagonCount;

        if (wasLocomotive || activeWagons.Count == 0 || remainingCargoWagons == 0)
        {
            TriggerGameOver();
            return;
        }

        UpdatePrimaryTarget();
    }

    public void DetachLastCargoWagon()
    {
        WagonController leftmostCargoWagon = null;
        float minX = float.MaxValue;
        int cargoCount = 0;

        foreach (var w in activeWagons)
        {
            if (w == null || w.IsLocomotive) continue;
            cargoCount++;
            float wx = w.GetWorldBounds().min.x;
            if (wx < minX)
            {
                minX = wx;
                leftmostCargoWagon = w;
            }
        }

        if (cargoCount > 1 && leftmostCargoWagon != null)
        {
            leftmostCargoWagon.DestroyOrDetachWagon();
        }
    }

    public WagonController GetClosestActiveWagon(Vector3 position)
    {
        WagonController closest = null;
        float minDistance = float.MaxValue;

        foreach (WagonController wagon in activeWagons)
        {
            if (wagon == null || !wagon.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(position, wagon.GetWorldBounds().center);
            if (dist < minDistance)
            {
                minDistance = dist;
                closest = wagon;
            }
        }
        return closest;
    }

    public void UpdatePrimaryTarget()
    {
        if (activeWagons.Count > 0 && activeWagons[activeWagons.Count - 1] != null)
        {
            currentTarget = activeWagons[activeWagons.Count - 1].transform;
        }

        foreach (EnemyFSM enemy in FindObjectsByType<EnemyFSM>(FindObjectsSortMode.None))
        {
            enemy.ChangeTarget();
        }
    }

    public void SetTarget(Transform newTarget)
    {
        currentTarget = newTarget;
        foreach (EnemyFSM enemy in FindObjectsByType<EnemyFSM>(FindObjectsSortMode.None))
        {
            enemy.ChangeTarget();
        }
    }

    public void CompleteZoneAndOpenSummary()
    {
        if (gameEnded) return;
        gameEnded = true;
        Time.timeScale = 1f;

        if (Application.CanStreamedLevelBeLoaded(levelSummaryScene))
        {
            SceneManager.LoadScene(levelSummaryScene);
        }
        else
        {
            showingInlineLevelSummary = true;
            Time.timeScale = 0f;
            if (GetComponent<LevelSummaryController>() == null)
            {
                gameObject.AddComponent<LevelSummaryController>();
            }
        }
    }

    public void TriggerVictory()
    {
        CompleteZoneAndOpenSummary();
    }

    public void TriggerGameOver()
    {
        if (gameEnded) return;
        gameEnded = true;
        Time.timeScale = 1f;
        RunProgressionData.ClearSavedRun();
        SceneManager.LoadScene(gameOverScene);
    }

    public void StartNewGameFromMenu()
    {
        Time.timeScale = 1f;
        RunProgressionData.StartNewRun();
        SceneManager.LoadScene(gameScene);
    }

    public void ContinueSavedGameFromMenu()
    {
        if (!RunProgressionData.HasSavedRun()) return;
        Time.timeScale = 1f;
        RunProgressionData.LoadFromDisk();
        SceneManager.LoadScene(gameScene);
    }

    public void LoadGameScene()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(gameScene);
    }

    public void LoadStartMenu()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(startMenuScene);
    }

    public void QuitGame()
    {
        Time.timeScale = 1f;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != startMenuScene || continueSavedGameButton != null)
            return;

        bool hasSave = RunProgressionData.HasSavedRun();
        GUI.enabled = hasSave;

        GUIStyle btnStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 20,
            fontStyle = FontStyle.Bold
        };

        float bw = 380f;
        float bh = 58f;
        float bx = (Screen.width - bw) * 0.5f;
        float by = Screen.height * 0.72f;

        string label = hasSave
            ? $"▶ Continuar Partida (Zona {RunProgressionData.State.currentZoneLevel})"
            : "🔒 Continuar Partida (Sin datos guardados)";

        if (GUI.Button(new Rect(bx, by, bw, bh), label, btnStyle))
        {
            ContinueSavedGameFromMenu();
        }

        GUI.enabled = true;
    }
}