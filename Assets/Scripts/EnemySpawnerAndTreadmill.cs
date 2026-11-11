using System.Collections.Generic;
using UnityEngine;

public class EnemySpawnerAndTreadmill : MonoBehaviour
{
    [System.Serializable]
    private class EnemySpawnSettings
    {
        public GameObject enemyPrefab;

        [Min(0.01f)]
        [Tooltip("Segundos entre apariciones de este enemigo")]
        public float spawnInterval = 3.5f;

        [System.NonSerialized]
        public float nextSpawnTime;
    }

    [Header("Spawner de Oleadas de Zombies")]
    [SerializeField] private List<EnemySpawnSettings> enemyTypes = new List<EnemySpawnSettings>();
    [SerializeField] private Transform[] spawnPoints;

    [Header("Efecto Treadmill + Vías Paralelas (Z=0 y Z=10) + Variación Procedural Ligera")]
    [Tooltip("Si lo dejas vacío, el script genera automáticamente las 2 vías de tren y adornos procedurales (árboles, rocas, arbustos)")]
    [SerializeField] private List<Transform> scrollingProps = new List<Transform>();
    [Tooltip("Opcional: arrastra aquí prefabs de árboles, piedras o arbustos para que se mezclen proceduralmente")]
    [SerializeField] private GameObject[] customNaturePropPrefabs;
    [SerializeField] private bool autoGenerateProceduralEnvironment = true;
    [SerializeField] private float baseScrollSpeed = 14f;
    [SerializeField] private float resetLimitX = -50f;
    [SerializeField] private float respawnStartX = 50f;
    [SerializeField] private float enemyTrainTrackZ = 10f;
    [SerializeField] private Renderer floorRendererToScrollUV;
    [SerializeField] private Vector2 uvScrollDirection = new Vector2(0.5f, 0f);

    private List<Transform> randomizeOnWrapProps = new List<Transform>();
    private Vector2 currentUVOffset;

    void Start()
    {
        // Asegurar que exista el controlador del Tren Enemigo en la escena automáticamente
        if (GetComponent<EnemyTrainController>() == null && FindFirstObjectByType<EnemyTrainController>() == null)
        {
            gameObject.AddComponent<EnemyTrainController>();
        }

        if (autoGenerateProceduralEnvironment && (scrollingProps == null || scrollingProps.Count == 0))
        {
            GenerateTracksAndProceduralScenery();
        }
    }

    private void GenerateTracksAndProceduralScenery()
    {
        scrollingProps = new List<Transform>();
        randomizeOnWrapProps = new List<Transform>();
        GameObject container = new GameObject("_Treadmill_TracksAndProceduralScenery");

        Color sleeperColor = new Color(0.28f, 0.20f, 0.14f, 1f);

        // 1. Generar Durmientes de Vía para NUESTRO TREN (Z = 0) y para las VÍAS PARALELAS DEL TREN ENEMIGO (Z = 10)
        for (float x = resetLimitX; x < respawnStartX; x += 4.5f)
        {
            GameObject tieOurTrain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tieOurTrain.name = "TrackTie_Player_Z0";
            tieOurTrain.transform.SetParent(container.transform);
            tieOurTrain.transform.position = new Vector3(x, 0.05f, 0f);
            tieOurTrain.transform.localScale = new Vector3(0.7f, 0.1f, 5.6f);
            tieOurTrain.GetComponent<Renderer>().material.color = sleeperColor;
            Destroy(tieOurTrain.GetComponent<Collider>());
            scrollingProps.Add(tieOurTrain.transform);

            GameObject tieEnemyTrain = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tieEnemyTrain.name = "TrackTie_Enemy_Z10";
            tieEnemyTrain.transform.SetParent(container.transform);
            tieEnemyTrain.transform.position = new Vector3(x, 0.05f, enemyTrainTrackZ);
            tieEnemyTrain.transform.localScale = new Vector3(0.7f, 0.1f, 5.6f);
            tieEnemyTrain.GetComponent<Renderer>().material.color = sleeperColor;
            Destroy(tieEnemyTrain.GetComponent<Collider>());
            scrollingProps.Add(tieEnemyTrain.transform);
        }

        // 2. Generar Adornos Procedurales (Árboles, Piedras y Arbustos) que cambian de escala, rotación y posición Z cada vez que dan la vuelta
        int zone = RunProgressionData.State.currentZoneLevel;
        for (int i = 0; i < 24; i++)
        {
            float startX = Random.Range(resetLimitX, respawnStartX);
            bool northSide = (i % 2 == 0);
            float startZ = northSide ? Random.Range(16.5f, 27f) : Random.Range(-25f, -14f);

            GameObject prop = CreateProceduralNatureProp(container.transform, i % 3, zone);
            prop.transform.position = new Vector3(startX, 0f, startZ);
            RandomizePropAppearance(prop.transform, northSide);

            scrollingProps.Add(prop.transform);
            randomizeOnWrapProps.Add(prop.transform);
        }
    }

    private GameObject CreateProceduralNatureProp(Transform parent, int propType, int zoneLevel)
    {
        if (customNaturePropPrefabs != null && customNaturePropPrefabs.Length > 0)
        {
            GameObject custom = Instantiate(customNaturePropPrefabs[Random.Range(0, customNaturePropPrefabs.Length)], parent);
            foreach (Collider c in custom.GetComponentsInChildren<Collider>()) Destroy(c);
            return custom;
        }

        GameObject root = new GameObject($"ProceduralProp_Type{propType}");
        root.transform.SetParent(parent);

        // Variar ligeramente el tono de vegetación/roca según el Nivel/Zona actual para sentir progresión entre zonas
        float zoneHueShift = ((zoneLevel - 1) % 4) * 0.08f;

        if (propType == 0)
        {
            // Árbol (Tronco + Copa)
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            trunk.transform.SetParent(root.transform, false);
            trunk.transform.localPosition = new Vector3(0f, 1.2f, 0f);
            trunk.transform.localScale = new Vector3(0.6f, 1.2f, 0.6f);
            trunk.GetComponent<Renderer>().material.color = new Color(0.38f, 0.24f, 0.14f, 1f);
            Destroy(trunk.GetComponent<Collider>());

            GameObject foliage = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            foliage.transform.SetParent(root.transform, false);
            foliage.transform.localPosition = new Vector3(0f, 3.1f, 0f);
            foliage.transform.localScale = new Vector3(2.4f, 2.6f, 2.4f);
            foliage.GetComponent<Renderer>().material.color = new Color(0.18f + zoneHueShift, 0.42f - zoneHueShift * 0.5f, 0.18f, 1f);
            Destroy(foliage.GetComponent<Collider>());
        }
        else if (propType == 1)
        {
            // Piedra / Roca
            GameObject rock = GameObject.CreatePrimitive(PrimitiveType.Cube);
            rock.transform.SetParent(root.transform, false);
            rock.transform.localPosition = new Vector3(0f, 0.8f, 0f);
            rock.transform.localScale = new Vector3(2.0f, 1.5f, 1.8f);
            rock.transform.localRotation = Quaternion.Euler(12f, 35f, 8f);
            rock.GetComponent<Renderer>().material.color = new Color(0.42f, 0.44f, 0.46f, 1f);
            Destroy(rock.GetComponent<Collider>());
        }
        else
        {
            // Arbusto
            GameObject bush = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bush.transform.SetParent(root.transform, false);
            bush.transform.localPosition = new Vector3(0f, 0.6f, 0f);
            bush.transform.localScale = new Vector3(1.8f, 1.2f, 1.6f);
            bush.GetComponent<Renderer>().material.color = new Color(0.24f + zoneHueShift, 0.48f - zoneHueShift * 0.4f, 0.20f, 1f);
            Destroy(bush.GetComponent<Collider>());
        }

        return root;
    }

    private void RandomizePropAppearance(Transform prop, bool northSide)
    {
        float newZ = northSide ? Random.Range(16.5f, 28f) : Random.Range(-26f, -14f);
        float randomScale = Random.Range(0.75f, 1.55f);
        prop.position = new Vector3(prop.position.x, 0f, newZ);
        prop.localScale = Vector3.one * randomScale;
        prop.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
    }

    void Update()
    {
        if (GameManager.Instance != null && GameManager.Instance.isPaused) return;

        HandleEnemySpawning();
        HandleTreadmillScrolling();
    }

    private void HandleEnemySpawning()
    {
        // Si el Tren Enemigo está activo en las vías paralelas, pausar la aparición de zombies comunes
        if (EnemyTrainController.Instance != null && EnemyTrainController.Instance.IsPauseZombieSpawningActive)
        {
            return;
        }

        if (enemyTypes == null) return;

        // Escalar ligeramente la frecuencia de aparición en zonas más avanzadas para mantener el reto divertido
        int zone = RunProgressionData.State.currentZoneLevel;
        float zoneDifficultyMult = Mathf.Max(0.5f, 1f - (zone - 1) * 0.10f);

        foreach (EnemySpawnSettings enemy in enemyTypes)
        {
            if (enemy == null || enemy.enemyPrefab == null || Time.time < enemy.nextSpawnTime)
                continue;

            enemy.nextSpawnTime = Time.time + Mathf.Max(0.25f, enemy.spawnInterval * zoneDifficultyMult);

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

            Instantiate(enemy.enemyPrefab, spawnPos, Quaternion.identity);
        }
    }

    private void HandleTreadmillScrolling()
    {
        float speedMult = GameManager.Instance != null
            ? GameManager.Instance.GetTrainScrollSpeedMultiplier()
            : 1f;
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
                    pos.x = respawnStartX + Random.Range(0f, 3.5f);
                    prop.position = pos;

                    // Si es un adorno natural, cambiar su escala, giro y profundidad Z al reciclarse para que nunca se vea repetitivo
                    if (randomizeOnWrapProps != null && randomizeOnWrapProps.Contains(prop))
                    {
                        bool isNorth = prop.position.z > 5f;
                        RandomizePropAppearance(prop, isNorth);
                    }
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