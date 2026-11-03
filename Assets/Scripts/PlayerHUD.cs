using UnityEngine;
using UnityEngine.UI;

public class PlayerHUD : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider distanceSlider;

    [Header("Journey")]
    [Tooltip("Total time in seconds the full journey takes.")]
    [SerializeField] private float totalTime = 60f;
    [Tooltip("Total distance represented by a full slider.")]
    [SerializeField] private float totalDistance = 1000f;
    [Tooltip("Use Time.timeSinceLevelLoad instead of an internal timer.")]
    [SerializeField] private bool useGlobalTime = false;

    private float elapsed;

    public float DistanceTraveled { get; private set; }

    private void Start()
    {
        if (distanceSlider != null)
        {
            distanceSlider.minValue = 0f;
            distanceSlider.maxValue = totalDistance;
        }
    }

    private void Update()
    {
        elapsed = useGlobalTime ? Time.timeSinceLevelLoad : elapsed + Time.deltaTime;

        // Constant speed = totalDistance / totalTime
        float speed = totalDistance / Mathf.Max(totalTime, 0.0001f);
        DistanceTraveled = Mathf.Clamp(elapsed * speed, 0f, totalDistance);

        if (distanceSlider != null)
            distanceSlider.value = DistanceTraveled;
    }
}
