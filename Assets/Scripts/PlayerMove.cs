using UnityEngine;

public enum TrainDeckZone
{
    PlayerTrain,       // Caminando DENTRO de los vagones de nuestro tren (piso interior)
    EnemyTrainBoarding // Cuando salta al tren enemigo paralelo en Z = 10
}

public class PlayerMove : MonoBehaviour
{
    [Header("Movimiento Dentro de los Vagones (WASD)")]
    [SerializeField] private float speed = 7.5f;
    [Tooltip("Margen respecto a las paredes/bordes del vagón para que el Player no atraviese los pilares ni se caiga")]
    [SerializeField] private float interiorWallPadding = 0.55f;
    [Tooltip("True = Camina sobre el piso INTERIOR del vagón (dejando el techo exclusivo para torretas)")]
    [SerializeField] private bool walkInsideWagonFloor = true;
    [Tooltip("Altura del Player respecto al piso interior del vagón (ajústalo en el Inspector si tu piso es más grueso)")]
    [SerializeField] private float playerInteriorFloorOffsetY = 1.15f;

    [Header("Abordaje a Tren Enemigo Paralelo (Z = 10)")]
    public TrainDeckZone currentZone = TrainDeckZone.PlayerTrain;
    public bool canBoardEnemyTrain = false;
    public KeyCode jumpBetweenTrainsKey = KeyCode.Space;
    public Bounds enemyTrainRoofBounds = new Bounds(new Vector3(-5f, 1.1f, 10f), new Vector3(20f, 0.4f, 4.6f));

    private Rigidbody rb;
    private bool pendingUiJump = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
            rb.constraints = RigidbodyConstraints.FreezeRotation;
        }
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        float moveX = 0f;
        float moveZ = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) moveX -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) moveX += 1f;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) moveZ += 1f;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) moveZ -= 1f;

        Vector3 moveDir = new Vector3(moveX, 0f, moveZ);
        if (moveDir.sqrMagnitude > 1f) moveDir.Normalize();

        Vector3 nextPos = transform.position + moveDir * speed * Time.deltaTime;

        if (Input.GetKeyDown(jumpBetweenTrainsKey) || pendingUiJump)
        {
            pendingUiJump = false;
            TryExecuteTrainJump(ref nextPos);
        }

        if (currentZone == TrainDeckZone.PlayerTrain)
        {
            ClampToPlayerTrainInterior(ref nextPos);
        }
        else if (currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            ClampToBounds(ref nextPos, enemyTrainRoofBounds);
        }

        transform.position = nextPos;
    }

    public bool IsNearBoardingEdge(out string actionLabel)
    {
        actionLabel = "";
        if (!canBoardEnemyTrain) return false;

        if (currentZone == TrainDeckZone.PlayerTrain)
        {
            if (TryGetCombinedPlayerTrainBounds(out Bounds ourBounds))
            {
                float distToNorthEdge = Mathf.Abs(transform.position.z - (ourBounds.max.z - interiorWallPadding));
                if (distToNorthEdge <= 1.6f)
                {
                    actionLabel = "🚀 SALTAR AL TREN ENEMIGO";
                    return true;
                }
            }
        }
        else if (currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            float distToSouthEdge = Mathf.Abs(transform.position.z - (enemyTrainRoofBounds.min.z + interiorWallPadding));
            if (distToSouthEdge <= 1.6f)
            {
                actionLabel = "🔙 SALTAR DE REGRESO A TU TREN";
                return true;
            }
        }
        return false;
    }

    public void TriggerBoardingJumpFromUI()
    {
        pendingUiJump = true;
    }

    private void TryExecuteTrainJump(ref Vector3 currentPos)
    {
        if (currentZone == TrainDeckZone.PlayerTrain && canBoardEnemyTrain)
        {
            if (TryGetCombinedPlayerTrainBounds(out Bounds ourBounds))
            {
                float distToNorthEdge = Mathf.Abs(currentPos.z - (ourBounds.max.z - interiorWallPadding));
                if (distToNorthEdge <= 1.8f)
                {
                    currentZone = TrainDeckZone.EnemyTrainBoarding;
                    currentPos.z = enemyTrainRoofBounds.min.z + interiorWallPadding + 0.2f;
                    currentPos.x = Mathf.Clamp(currentPos.x, enemyTrainRoofBounds.min.x + 1f, enemyTrainRoofBounds.max.x - 1f);
                    Debug.Log("[Abordaje] ¡Saltaste al interior del tren enemigo! Camina hacia su locomotora para recoger el Drop.");
                }
            }
        }
        else if (currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            float distToSouthEdge = Mathf.Abs(currentPos.z - (enemyTrainRoofBounds.min.z + interiorWallPadding));
            if (distToSouthEdge <= 1.8f && TryGetCombinedPlayerTrainBounds(out Bounds ourBounds))
            {
                currentZone = TrainDeckZone.PlayerTrain;
                currentPos.z = ourBounds.max.z - interiorWallPadding - 0.2f;
                currentPos.x = Mathf.Clamp(currentPos.x, ourBounds.min.x + 1f, ourBounds.max.x - 1f);
                Debug.Log("[Abordaje] ¡Regresaste a salvo a tu tren!");
            }
        }
    }

    private void ClampToPlayerTrainInterior(ref Vector3 pos)
    {
        if (TryGetCombinedPlayerTrainBounds(out Bounds trainBounds))
        {
            ClampToBounds(ref pos, trainBounds);
        }
    }

    private void ClampToBounds(ref Vector3 pos, Bounds bounds)
    {
        float minX = bounds.min.x + interiorWallPadding;
        float maxX = bounds.max.x - interiorWallPadding;
        float minZ = bounds.min.z + interiorWallPadding;
        float maxZ = bounds.max.z - interiorWallPadding;

        pos.x = Mathf.Clamp(pos.x, minX, maxX);
        pos.z = Mathf.Clamp(pos.z, minZ, maxZ);

        // Si walkInsideWagonFloor es true, el Player camina en el piso interior (bounds.min.y + offset), dejando el techo libre para las torretas
        if (walkInsideWagonFloor)
        {
            pos.y = bounds.min.y + playerInteriorFloorOffsetY;
        }
        else
        {
            pos.y = bounds.max.y + playerInteriorFloorOffsetY;
        }
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

    public void SetEnemyTrainBoardingWindow(bool enabled, Bounds enemyBounds)
    {
        canBoardEnemyTrain = enabled;
        enemyTrainRoofBounds = enemyBounds;

        if (!enabled && currentZone == TrainDeckZone.EnemyTrainBoarding)
        {
            currentZone = TrainDeckZone.PlayerTrain;
        }
    }
}