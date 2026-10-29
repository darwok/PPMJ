using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Referencias de Sistemas")]
    [Tooltip("Puedes arrastrar aquí el componente CardDeckManager (o ponerlo en este mismo objeto _GameManager)")]
    public CardDeckManager cardDeckManager;

    [Header("Lista de Vagones Activos (Ordenados de Punta a Cola)")]
    public List<WagonController> activeWagons = new List<WagonController>();
    public Transform currentTarget;

    [Header("Progreso de la Ruta (Vertical Slice: 3 a 5 min)")]
    [SerializeField] private float routeDurationSeconds = 180f;
    private float currentRouteTime = 0f;
    private bool gameEnded = false;

    [Header("Balance de Potencia de la Locomotora (Tecla Q)")]
    [Tooltip("False = Modo Aceleración (tren más rápido), True = Sobrecarga Defensiva (torretas disparan más rápido)")]
    public bool isWeaponOverchargeActive = false;
    [SerializeField] private float speedMultiplierWhenAccelerating = 1.35f;
    [SerializeField] private float turretFireRateMultiplierWhenOvercharged = 1.5f;

    [Header("Perks Globales del Tren (Recompensas por Abordar Tren Enemigo)")]
    [Tooltip("Bonificación acumulable de velocidad de ataque (+0.2, etc.)")]
    public float globalAttackSpeedBonus = 0f;
    [Tooltip("Bonificación acumulable de rango de ataque para las torretas")]
    public float globalAttackRangeBonus = 0f;

    [Header("Nombres de Escenas")]
    [SerializeField] private string startMenuScene = "StartMenu";
    [SerializeField] private string gameScene = "GameScene";
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

        if (activeWagons.Count == 0)
        {
            activeWagons.AddRange(FindObjectsByType<WagonController>(FindObjectsSortMode.InstanceID));
        }
        UpdatePrimaryTarget();
    }

    void Update()
    {
        if (gameEnded) return;

        // Alternar modo de energía de la locomotora con la tecla Q
        if (Input.GetKeyDown(KeyCode.Q))
        {
            isWeaponOverchargeActive = !isWeaponOverchargeActive;
            string modeName = isWeaponOverchargeActive ? "SOBRECARGA DE TORRETAS" : "ACELERACIÓN DE LOCOMOTORA";
            Debug.Log($"[Locomotora] Modo de energía cambiado a: {modeName}");
        }

        // Tecla G para desacoplar voluntariamente el último vagón de carga
        if (Input.GetKeyDown(KeyCode.G))
        {
            DetachLastCargoWagon();
        }

        float speedFactor = isWeaponOverchargeActive ? 1f : speedMultiplierWhenAccelerating;
        currentRouteTime += Time.deltaTime * speedFactor;

        if (currentRouteTime >= routeDurationSeconds)
        {
            TriggerVictory();
        }
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

    // Llamar a este método cuando el Player recolecte la recompensa en la locomotora del tren enemigo
    public void ApplyGlobalTrainPerk(float extraAttackSpeed, float extraAttackRange)
    {
        globalAttackSpeedBonus += extraAttackSpeed;
        globalAttackRangeBonus += extraAttackRange;
        Debug.Log($"[Perk Recolectado] Nuevo bono global -> Attack Speed: +{globalAttackSpeedBonus:F2}, Range: +{globalAttackRangeBonus:F1}");
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
            Debug.Log($"[Player] Desacoplando voluntariamente el último vagón: {leftmostCargoWagon.name}");
            leftmostCargoWagon.DestroyOrDetachWagon();
        }
        else
        {
            Debug.Log("[Player] No puedes desacoplar el último vagón de suministros.");
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

    public void TriggerVictory()
    {
        if (gameEnded) return;
        gameEnded = true;
        Debug.Log("¡VICTORIA! El tren llegó a la estación San Paula.");
        SceneManager.LoadScene(victoryScene);
    }

    public void TriggerGameOver()
    {
        if (gameEnded) return;
        gameEnded = true;
        Debug.Log("DERROTA: El convoy fue destruido.");
        SceneManager.LoadScene(gameOverScene);
    }

    // Métodos públicos para los botones de UI en StartMenu, Victory y GameOver
    public void LoadGameScene() => SceneManager.LoadScene(gameScene);
    public void LoadStartMenu() => SceneManager.LoadScene(startMenuScene);

    // Funciona tanto en el Editor de Unity (detiene el Play Mode) como en el ejecutable final (.exe)
    public void QuitGame()
    {
        Debug.Log("Saliendo del juego...");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}