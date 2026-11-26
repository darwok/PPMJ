using DG.Tweening;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
#endif

public class TimeManager : MonoBehaviour
{
    public static TimeManager Instance { get; private set; }
    public static bool IsPaused => Instance != null && Instance.isPaused;

    [Header("UI Referencias")]
    [SerializeField] private CanvasGroup pauseScreen;
    [SerializeField] private float pauseTweenTime = 0.25f;

    [Header("Nombres de Escenas (TREN 2112)")]
    [SerializeField] private string startMenuScene = "StartMenu";
    [SerializeField]
    private string[] nonPausableScenes = new string[]
    {
        "StartMenu",
        "MainMenu",
        "LevelSummary",
        "Victory",
        "GameOver"
    };

    private bool isPaused = false;
    private Tween pauseTween;
    private Coroutine freezeFrameRoutine;
    private float lastToggleTime = -1f;

    private GameObject playerRef;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void AutoInit()
    {
        if (Instance == null && Object.FindFirstObjectByType<TimeManager>() == null)
        {
            GameObject tmGO = new GameObject("TimeManager");
            tmGO.AddComponent<TimeManager>();
            DontDestroyOnLoad(tmGO);
        }
    }

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        InitializeForScene();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        InitializeForScene();
    }

    private bool IsNonPausableScene(string sceneName)
    {
        if (nonPausableScenes == null) return false;
        for (int i = 0; i < nonPausableScenes.Length; i++)
        {
            if (sceneName == nonPausableScenes[i]) return true;
        }
        return false;
    }

    private void InitializeForScene()
    {
        pauseTween?.Kill();
        if (freezeFrameRoutine != null) StopCoroutine(freezeFrameRoutine);

        isPaused = false;
        Time.timeScale = 1f;
        SyncGameManagerPause(false);

        pauseScreen = null;
        EnsurePauseScreen();

        if (pauseScreen != null)
        {
            pauseScreen.alpha = 0f;
            pauseScreen.blocksRaycasts = false;
            pauseScreen.interactable = false;
            pauseScreen.gameObject.SetActive(false);
        }

        EnsureEventSystem();
        playerRef = null;
        FindPlayer();

        // En TREN 2112 el cursor siempre debe estar libre y visible para apuntar y elegir cartas
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        string currentScene = SceneManager.GetActiveScene().name;
        if (!IsNonPausableScene(currentScene))
        {
            LockPlayerForTransition(false);
        }
    }

    private void FindPlayer()
    {
        if (playerRef == null)
        {
            PlayerMove pm = Object.FindFirstObjectByType<PlayerMove>();
            if (pm != null) playerRef = pm.gameObject;
            else playerRef = GameObject.FindGameObjectWithTag("Player");
        }
    }

    public CanvasGroup EnsurePauseScreen()
    {
        if (pauseScreen != null && pauseScreen.gameObject != null) return pauseScreen;

        // Buscar primero en cualquier Canvas de la escena un hijo llamado "PauseScreen"
        Canvas[] canvases = Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (Canvas c in canvases)
        {
            Transform psT = c.transform.Find("PauseScreen");
            if (psT != null)
            {
                pauseScreen = psT.GetComponent<CanvasGroup>();
                if (pauseScreen == null) pauseScreen = psT.gameObject.AddComponent<CanvasGroup>();
                break;
            }
        }

        if (pauseScreen == null)
        {
            CanvasGroup[] groups = Object.FindObjectsByType<CanvasGroup>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var cg in groups)
            {
                if (cg.gameObject.name.ToLower().Contains("pause"))
                {
                    pauseScreen = cg;
                    break;
                }
            }
        }

        if (pauseScreen != null)
        {
            WirePauseButtons();
        }

        return pauseScreen;
    }

    private void WirePauseButtons()
    {
        Button[] botones = pauseScreen.GetComponentsInChildren<Button>(true);
        foreach (var btn in botones)
        {
            string btnName = btn.gameObject.name.ToLower();

            if (btnName.Contains("resume") || btnName.Contains("reanudar") || btnName.Contains("continue"))
            {
                btn.onClick.RemoveListener(ResumeGame);
                btn.onClick.AddListener(ResumeGame);
            }
            else if (btnName.Contains("mainmenu") || btnName.Contains("startmenu") || btnName.Contains("menu"))
            {
                btn.onClick.RemoveListener(ReturnToStartMenu);
                btn.onClick.AddListener(ReturnToStartMenu);
            }
            else if (btnName.Contains("tutorial"))
            {
                btn.onClick.RemoveListener(OpenTutorialFromPause);
                btn.onClick.AddListener(OpenTutorialFromPause);
            }
        }
    }

    public static void EnsureEventSystem()
    {
        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            GameObject esGO = new GameObject("EventSystem");
            esGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            esGO.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();
#else
            esGO.AddComponent<StandaloneInputModule>();
#endif
        }
    }

    private void Update()
    {
        string currentScene = SceneManager.GetActiveScene().name;
        if (IsNonPausableScene(currentScene))
        {
            if (isPaused) TogglePause(false);
            return;
        }

        bool pausePressed = false;

#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.escapeKey.wasPressedThisFrame || Keyboard.current.pKey.wasPressedThisFrame)
            {
                pausePressed = true;
            }
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P))
        {
            pausePressed = true;
        }
#endif

        if (pausePressed && (Time.unscaledTime - lastToggleTime > 0.2f))
        {
            lastToggleTime = Time.unscaledTime;
            TogglePause(!isPaused);
        }
    }

    public void TogglePause(bool pause)
    {
        if (freezeFrameRoutine != null) StopCoroutine(freezeFrameRoutine);

        isPaused = pause;
        pauseTween?.Kill();

        EnsurePauseScreen();
        EnsureEventSystem();
        SyncGameManagerPause(isPaused);

        if (isPaused)
        {
            Time.timeScale = 0f;
            LockPlayerForTransition(true);

            if (pauseScreen != null)
            {
                pauseScreen.gameObject.SetActive(true);
                pauseScreen.blocksRaycasts = true;
                pauseScreen.interactable = true;
                pauseTween = pauseScreen.DOFade(1f, pauseTweenTime).SetUpdate(true);
            }
        }
        else
        {
            LockPlayerForTransition(false);

            if (pauseScreen != null)
            {
                pauseScreen.blocksRaycasts = false;
                pauseScreen.interactable = false;
                pauseTween = pauseScreen.DOFade(0f, pauseTweenTime)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        Time.timeScale = 1f;
                        pauseScreen.gameObject.SetActive(false);
                    });
            }
            else
            {
                Time.timeScale = 1f;
            }
        }
    }

    private void SyncGameManagerPause(bool paused)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isPaused = paused;
        }
    }

    public void LockPlayerForTransition(bool lockInput)
    {
        FindPlayer();

        if (!lockInput)
        {
            if (TutorialManager.Instance != null && TutorialManager.IsTutorialActive)
            {
                return;
            }
        }

        if (playerRef != null)
        {
            PlayerMove pMove = playerRef.GetComponentInChildren<PlayerMove>();
            if (pMove != null) pMove.enabled = !lockInput;

            PlayerShoot pShoot = playerRef.GetComponentInChildren<PlayerShoot>();
            if (pShoot != null) pShoot.enabled = !lockInput;
        }

        // Mantener siempre visible y libre el cursor en TREN 2112
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame() => TogglePause(false);

    public void ReturnToStartMenu()
    {
        pauseTween?.Kill();
        isPaused = false;
        Time.timeScale = 1f;
        SyncGameManagerPause(false);

        if (GameManager.Instance != null)
        {
            GameManager.Instance.LoadStartMenu();
        }
        else
        {
            SceneManager.LoadScene(startMenuScene);
        }
    }

    public void OpenTutorialFromPause()
    {
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.StartTutorialFromPause();
        }
        else
        {
            Debug.LogWarning("[TimeManager] No se encontró un TutorialManager en esta escena.");
        }
    }

    public void FreezeFrame(float timeScale, float duration)
    {
        if (freezeFrameRoutine != null) StopCoroutine(freezeFrameRoutine);
        freezeFrameRoutine = StartCoroutine(FreezeFrameRoutine(timeScale, duration));
    }

    private IEnumerator FreezeFrameRoutine(float targetScale, float duration)
    {
        Time.timeScale = targetScale;
        yield return new WaitForSecondsRealtime(duration);
        if (!isPaused) Time.timeScale = 1f;
    }
}