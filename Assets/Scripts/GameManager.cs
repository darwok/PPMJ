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

        // Si no se asignaron vagones manualmente en el Inspector, buscarlos en la escena
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

        // Tecla G para desacoplar voluntariamente el último vagón de carga (si hay más de 1 vagón de carga)
        if (Input.GetKeyDown(KeyCode.G))
        {
            DetachLastCargoWagon();
        }

        // Avanzar el progreso hacia la estación San Paula
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
        return isWeaponOverchargeActive ? turretFireRateMultiplierWhenOvercharged : 1f;
    }

    public float GetTrainScrollSpeedMultiplier()
    {
        return isWeaponOverchargeActive ? 0.85f : speedMultiplierWhenAccelerating;
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

        // Verificar condiciones de derrota: destrucción de la locomotora o pérdida de todos los vagones de carga
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

        // Actualizar el objetivo de todos los enemigos vivos para que ninguno se quede sin target
        UpdatePrimaryTarget();
    }

    public void DetachLastCargoWagon()
    {
        // Buscar el último vagón de la lista que no sea la locomotora
        for (int i = activeWagons.Count - 1; i >= 0; i--)
        {
            if (activeWagons[i] != null && !activeWagons[i].IsLocomotive)
            {
                // Solo permitir sacrificarlo si queda al menos otro vagón de carga
                int cargoCount = 0;
                foreach (var w in activeWagons)
                    if (w != null && !w.IsLocomotive) cargoCount++;

                if (cargoCount > 1)
                {
                    Debug.Log($"[Player] Desacoplando voluntariamente el vagón: {activeWagons[i].name}");
                    activeWagons[i].DestroyOrDetachWagon();
                }
                else
                {
                    Debug.Log("[Player] No puedes desacoplar el último vagón de suministros.");
                }
                break;
            }
        }
    }

    public WagonController GetClosestActiveWagon(Vector3 position)
    {
        WagonController closest = null;
        float minDistance = float.MaxValue;

        foreach (WagonController wagon in activeWagons)
        {
            if (wagon == null || !wagon.gameObject.activeInHierarchy) continue;

            float dist = Vector3.Distance(position, wagon.transform.position);
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
    public void QuitGame() => Application.Quit();
}