using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

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

    [Header("Progreso de la Ruta (Vertical Slice)")]
    [SerializeField] private float routeDurationSeconds = 180f;
    private float currentRouteTime = 0f;
    private bool gameEnded = false;
    private bool showingInlineLevelSummary = false;
    public bool isPaused = false;

    [Header("Balance de Potencia de la Locomotora (Tecla Q)")]
    public bool isWeaponOverchargeActive = false;
    [SerializeField] private float speedMultiplierWhenAccelerating = 1.35f;
    [SerializeField] private float turretFireRateMultiplierWhenOvercharged = 1.5f;

    [Header("Bonos Adicionales en Partida")]
    public float globalAttackSpeedBonus = 0f;
    public float globalAttackRangeBonus = 0f;

    [Header("Nombres de Escenas")]
    [SerializeField] private string startMenuScene = "StartMenu";
    [SerializeField] private string gameScene = "GameScene";
    [SerializeField] private string levelSummaryScene = "LevelSummary";
    [SerializeField] private string victoryScene = "Victory";
    [SerializeField] private string gameOverScene = "GameOver";

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

        // Si estamos en StartMenu y hay un botón de Continuar asignado, activarlo o desactivarlo (oscurecido) según haya partida guardada
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
        return isWeaponOverchargeActive ? 0.85f : speedMultiplierWhenAccelerating;
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

        int remainingCargoWagons = 0;
        foreach (WagonController w in activeWagons)
        {
            if (w != null && !w.IsLocomotive) remainingCargoWagons++;
        }

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

    // Abre la escena intermedia de resumen de nivel/perks (o la muestra en pantalla si aún no has creado el archivo LevelSummary.unity)
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
            // Si aún no has agregado la escena LevelSummary a Build Profiles, muestra la pantalla intermedia directamente aquí sin crashear
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

    // Inicia una partida nueva limpia desde Zona 1 (para el botón "Nueva Partida / Jugar" en StartMenu)
    public void StartNewGameFromMenu()
    {
        Time.timeScale = 1f;
        RunProgressionData.StartNewRun();
        SceneManager.LoadScene(gameScene);
    }

    // Continúa la partida guardada (para el botón "Continuar Partida" en StartMenu)
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

    // Si estamos en la escena StartMenu y aún no creaste el botón UI de Continuar, dibuja el botón automáticamente (oscurecido si no hay datos guardados)
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