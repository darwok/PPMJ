using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnerAndTreadmill : MonoBehaviour
{
    [Header("Spawner de Oleadas de Zombies")]
    [SerializeField] private GameObject enemyPrefab;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private float initialSpawnInterval = 3.5f;
    [SerializeField] private float minSpawnInterval = 1.0f;

    [Header("Efecto Treadmill (Ilusión de Movimiento del Tren)")]
    [Tooltip("Si lo dejas vacío, el script genera automáticamente durmientes de vía y postes para ver el efecto de movimiento")]
    [SerializeField] private List<Transform> scrollingProps = new List<Transform>();
    [SerializeField] private bool autoGenerateGreyboxPropsIfEmpty = true;
    [SerializeField] private float baseScrollSpeed = 14f;
    [SerializeField] private float resetLimitX = -45f;
    [SerializeField] private float respawnStartX = 45f;
    [SerializeField] private Renderer floorRendererToScrollUV;
    [SerializeField] private Vector2 uvScrollDirection = new Vector2(0.5f, 0f);

    private float nextSpawnTime;
    private Vector2 currentUVOffset;

    void Start()
    {
        // Si no se arrastraron objetos manualmente al arreglo Scrolling Props, crear bloques de referencia automáticamente
        if (autoGenerateGreyboxPropsIfEmpty && (scrollingProps == null || scrollingProps.Count == 0))
        {
            GenerateDefaultTreadmillProps();
        }
    }

    private void GenerateDefaultTreadmillProps()
    {
        scrollingProps = new List<Transform>();
        GameObject container = new GameObject("_Treadmill_EnvironmentProps");

        // Crear durmientes de vía y postes laterales fuera del paso de los enemigos para percibir la velocidad del tren
        for (float x = resetLimitX; x < respawnStartX; x += 9f)
        {
            // Poste / Roca superior (Z = +18)
            GameObject topProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            topProp.name = "Treadmill_Pillar_Top";
            topProp.transform.SetParent(container.transform);
            topProp.transform.position = new Vector3(x, 1.5f, 18f);
            topProp.transform.localScale = new Vector3(1.2f, 3f, 1.2f);
            Destroy(topProp.GetComponent<Collider>()); // Sin collider para no estorbar al NavMesh ni a las balas
            scrollingProps.Add(topProp.transform);

            // Poste / Roca inferior (Z = -22)
            GameObject bottomProp = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottomProp.name = "Treadmill_Pillar_Bottom";
            bottomProp.transform.SetParent(container.transform);
            bottomProp.transform.position = new Vector3(x + 4.5f, 1.5f, -22f);
            bottomProp.transform.localScale = new Vector3(1.2f, 3f, 1.2f);
            Destroy(bottomProp.GetComponent<Collider>());
            scrollingProps.Add(bottomProp.transform);
        }
    }

    void Update()
    {
        HandleEnemySpawning();
        HandleTreadmillScrolling();
    }

    private void HandleEnemySpawning()
    {
        if (enemyPrefab == null || Time.time < nextSpawnTime) return;

        float progress = GameManager.Instance != null ? GameManager.Instance.GetRouteProgressNormalized() : 0f;
        float currentInterval = Mathf.Lerp(initialSpawnInterval, minSpawnInterval, progress);
        nextSpawnTime = Time.time + currentInterval;

        Vector3 spawnPos;
        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform pt = spawnPoints[Random.Range(0, spawnPoints.Length)];
            spawnPos = pt.position;
        }
        else
        {
            float zSide = Random.value > 0.5f ? 14f : -18f;
            float xPos = Random.Range(-22f, 8f);
            spawnPos = new Vector3(xPos, 0.5f, zSide);
        }

        Instantiate(enemyPrefab, spawnPos, Quaternion.identity);
    }

    private void HandleTreadmillScrolling()
    {
        float speedMult = GameManager.Instance != null ? GameManager.Instance.GetTrainScrollSpeedMultiplier() : 1f;
        float currentSpeed = baseScrollSpeed * speedMult;

        if (scrollingProps != null)
        {
            for (int i = 0; i < scrollingProps.Count; i++)
            {
                Transform prop = scrollingProps[i];
                if (prop == null) continue;

                prop.Translate(Vector3.left * currentSpeed * Time.deltaTime, Space.World);

                if (prop.position.x <= resetLimitX)
                {
                    Vector3 pos = prop.position;
                    pos.x = respawnStartX;
                    prop.position = pos;
                }
            }
        }

        if (floorRendererToScrollUV != null && floorRendererToScrollUV.material != null)
        {
            currentUVOffset += uvScrollDirection * (currentSpeed * 0.05f) * Time.deltaTime;
            floorRendererToScrollUV.material.mainTextureOffset = currentUVOffset;
        }
    }
}