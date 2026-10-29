using UnityEngine;

public enum TrainDeckZone
{
    PlayerTrain,       // Sobre los vagones de nuestro tren (sin poder caerse)
    EnemyTrainBoarding // Cuando salta al tren enemigo paralelo (ej. en Z = 10)
}

public class PlayerMove : MonoBehaviour
{
    [Header("Movimiento sobre los Vagones (WASD)")]
    [SerializeField] private float speed = 7f;
    [Tooltip("Margen respecto al borde de los vagones para que el Player nunca se caiga")]
    [SerializeField] private float roofEdgePadding = 0.45f;
    [Tooltip("Altura del pivote del Player sobre la cara superior del techo del vagón")]
    [SerializeField] private float playerHeightOffsetOnRoof = 1.0f;

    [Header("Abordaje a Tren Enemigo Paralelo / Estación (Preparado para siguiente fase)")]
    public TrainDeckZone currentZone = TrainDeckZone.PlayerTrain;
    [Tooltip("Se activa automáticamente (true) durante 10 s cuando el tren enemigo en Z=10 se queda sin vida")]
    public bool canBoardEnemyTrain = false;
    public KeyCode jumpBetweenTrainsKey = KeyCode.Space;
    [Tooltip("Límites de la superficie del tren enemigo paralelo (por defecto en Z = 10)")]
    public Bounds enemyTrainRoofBounds = new Bounds(new Vector3(-5f, 3.5f, 10f), new Vector3(30f, 5f, 5f));

    private Rigidbody rb;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            // Evitar que la gravedad tire al Player o que rote al caminar sobre los vagones
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    void Update()
    {
        // Movimiento en 2 ejes sobre toda la superficie de los vagones (W/S para Z, A/D para X)
        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveX += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveZ += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveZ -= 1f;

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 nextPos = transform.position + moveDir * speed * Time.deltaTime;

        // Verificar si el jugador quiere saltar al tren enemigo paralelo (o regresar a nuestro tren)
        HandleTrainBoardingJump(ref nextPos);

        // Restringir (Clamp) la posición X, Y, Z para que camine libremente por los vagones sin caerse
        if (currentZone == TrainDeckZone.PlayerTrain)
        {
            ClampToPlayerTrainRoof(ref nextPos);
        }
        else if (currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            ClampToBounds(ref nextPos, enemyTrainRoofBounds);
        }

        transform.position = nextPos;
    }

    private void HandleTrainBoardingJump(ref Vector3 currentPos)
    {
        if (!Input.GetKeyDown(jumpBetweenTrainsKey)) return;

        if (currentZone == TrainDeckZone.PlayerTrain && canBoardEnemyTrain)
        {
            if (TryGetCombinedPlayerTrainBounds(out Bounds ourBounds))
            {
                // Solo permitir saltar si el Player se acercó a la orilla superior ("arriba", borde +Z) de nuestro tren
                float distToNorthEdge = Mathf.Abs(currentPos.z - (ourBounds.max.z - roofEdgePadding));
                if (distToNorthEdge <= 1.5f)
                {
                    currentZone = TrainDeckZone.EnemyTrainBoarding;
                    currentPos.z = enemyTrainRoofBounds.min.z + roofEdgePadding;
                    currentPos.y = enemyTrainRoofBounds.max.y + playerHeightOffsetOnRoof;
                    Debug.Log("[Abordaje] ¡Saltaste al tren enemigo! Corre a su locomotora por la recompensa.");
                }
            }
        }
        else if (currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            // Permitir saltar de regreso a nuestro tren desde la orilla inferior (-Z) del tren enemigo
            float distToSouthEdge = Mathf.Abs(currentPos.z - (enemyTrainRoofBounds.min.z + roofEdgePadding));
            if (distToSouthEdge <= 1.5f && TryGetCombinedPlayerTrainBounds(out Bounds ourBounds))
            {
                currentZone = TrainDeckZone.PlayerTrain;
                currentPos.z = ourBounds.max.z - roofEdgePadding;
                currentPos.y = ourBounds.max.y + playerHeightOffsetOnRoof;
                Debug.Log("[Abordaje] Regresaste a salvo a tu tren.");
            }
        }
    }

    private void ClampToPlayerTrainRoof(ref Vector3 pos)
    {
        if (TryGetCombinedPlayerTrainBounds(out Bounds trainBounds))
        {
            ClampToBounds(ref pos, trainBounds);
        }
    }

    private void ClampToBounds(ref Vector3 pos, Bounds bounds)
    {
        float minX = bounds.min.x + roofEdgePadding;
        float maxX = bounds.max.x - roofEdgePadding;
        float minZ = bounds.min.z + roofEdgePadding;
        float maxZ = bounds.max.z - roofEdgePadding;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);
        pos.y = bounds.max.y + playerHeightOffsetOnRoof;
    }

    private bool TryGetCombinedPlayerTrainBounds(out Bounds combinedBounds)
    {
        combinedBounds = new Bounds();
        if (GameManager.Instance == null || GameManager.Instance.activeWagons.Count == 0)
            return false;

        bool initialized = false;
        foreach (WagonController w in GameManager.Instance.activeWagons)
        {
            if (w == null || !w.gameObject.activeInHierarchy) continue;

            // Si quieres que el Player solo camine sobre los vagones de carga y no sobre la locomotora,
            // puedes descomentar la siguiente línea:
            // if (w.IsLocomotive) continue;

            Bounds wb = w.GetWorldBounds();
            if (!initialized)
            {
                combinedBounds = wb;
                initialized = true;
            }
            else
            {
                combinedBounds.Encapsulate(wb);
            }
        }
        return initialized;
    }

    // Llamar a este método cuando el tren enemigo sea destruido para abrir o cerrar la ventana de 10 s de abordaje
    public void SetEnemyTrainBoardingWindow(bool enabled, Bounds enemyBounds)
    {
        canBoardEnemyTrain = enabled;
        enemyTrainRoofBounds = enemyBounds;

        // Si se acabaron los 10 segundos y el jugador seguía en el tren enemigo, regresarlo al tren propio
        if (!enabled && currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            currentZone = TrainDeckZone.PlayerTrain;
        }
    }
}