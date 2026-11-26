using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }
    public static bool IsTutorialActive { get; private set; }

    [Header("Configuración")]
    [Tooltip("Actívalo si quieres que el tutorial aparezca automáticamente al iniciar GameScene")]
    [SerializeField] private bool showOnStart = false;

    [Header("UI References")]
    [SerializeField] private GameObject tutorialPanel;
    [SerializeField] private TextMeshProUGUI tutorialText;
    [SerializeField] private Button continueButton;
    [SerializeField] private Button skipButton;

    [Header("Pasos del Tutorial (TREN 2112)")]
    [TextArea(2, 4)]
    [SerializeField]
    private string[] tutorialSteps = new string[]
    {
        "¡Bienvenido a TREN 2112!\nUsa <b>WASD</b> para caminar dentro de los vagones y apunta con el ratón (<b>Clic Izq</b>) para disparar a los zombies. Recarga con <b>X</b>.",
        "Cada pocos segundos recibirás cartas en tu mano. Presiona <b>1, 2, 3 o 4</b> (o da clic en la carta) y luego haz clic en el techo de un vagón para colocar torretas o acoplar nuevos vagones.",
        "Presiona <b>V</b> (o el botón de vista) para cambiar entre seguir al jugador y la vista <b>Overview</b> para ver todos tus vagones.",
        "Presiona <b>Q</b> para alternar la energía de la locomotora entre <b>Aceleración</b> y <b>Sobrecarga de Torretas</b>. Con <b>G</b> puedes soltar tu último vagón si está por caer.",
        "Cuando aparezca el <b>Tren Enemigo</b> en las vías paralelas, destrúyelo antes de que escape, acércate a la orilla norte y presiona <b>Espacio</b> para saltar y recoger el Perk junto a su locomotora.",
        "¡Listo! Protege tus vagones hasta llegar a la estación. Puedes volver a ver este tutorial desde el Menú de Pausa (<b>ESC</b> o <b>P</b>)."
    };

    private int currentStep = 0;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        IsTutorialActive = false;
    }

    private void Start()
    {
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(NextStep);
        }

        if (skipButton != null)
        {
            skipButton.onClick.RemoveAllListeners();
            skipButton.onClick.AddListener(EndTutorial);
        }

        if (showOnStart)
        {
            StartTutorial();
        }
        else if (tutorialPanel != null)
        {
            tutorialPanel.SetActive(false);
        }
    }

    private void Update()
    {
        if (!IsTutorialActive) return;

        bool advancePressed = false;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame))
        {
            advancePressed = true;
        }
#endif
#if ENABLE_LEGACY_INPUT_MANAGER
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetKeyDown(KeyCode.F))
        {
            advancePressed = true;
        }
#endif

        if (advancePressed)
        {
            NextStep();
        }
    }

    // Llamado desde el botón Tutorial del Menú de Pausa
    public void StartTutorialFromPause()
    {
        IsTutorialActive = true;
        if (TimeManager.Instance != null) TimeManager.Instance.ResumeGame();
        StartTutorial();
    }

    public void StartTutorial()
    {
        currentStep = 0;
        IsTutorialActive = true;

        if (tutorialPanel != null) tutorialPanel.SetActive(true);

        LockPlayer(true);
        ShowStep();
    }

    private void ShowStep()
    {
        if (tutorialSteps == null || tutorialSteps.Length == 0) return;

        if (currentStep < tutorialSteps.Length)
        {
            if (tutorialText != null)
            {
                tutorialText.text = tutorialSteps[currentStep];
            }

            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
        else
        {
            EndTutorial();
        }
    }

    public void NextStep()
    {
        currentStep++;
        ShowStep();
    }

    private void EndTutorial()
    {
        IsTutorialActive = false;

        if (tutorialPanel != null) tutorialPanel.SetActive(false);

        LockPlayer(false);
    }

    private void LockPlayer(bool lockInput)
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.isPaused = lockInput;
        }

        Time.timeScale = lockInput ? 0f : 1f;

        PlayerMove pMove = Object.FindFirstObjectByType<PlayerMove>();
        if (pMove != null) pMove.enabled = !lockInput;

        PlayerShoot pShoot = Object.FindFirstObjectByType<PlayerShoot>();
        if (pShoot != null) pShoot.enabled = !lockInput;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}