
using Game.Scripts.GameLogic;
using Unity.Netcode;
using UnityEngine;
using TMPro;

public class MenuManager : MonoBehaviour
{
    [Header("Menu References")]
    public GameObject menuCam;
    public GameObject canvas;
    public GameObject lobbyMenu;
    public GameObject pauseMenu;
    public GameObject hud;
    public GameObject scoreboard;

    [Header("Relay UI")]
    [SerializeField] private RelayHostJoinManager relayManager;
    [SerializeField] private TMP_Text discoveryStatusText;
    [SerializeField] private TMP_Text joinCodeDisplayText;
    [SerializeField] private TMP_InputField joinCodeInput;
    [SerializeField] private GameObject joinFoundGameButton;

    public static MenuManager Instance { get; private set; }

    private NetworkManager networkManager;
    private bool connectionAttempted;
    private bool enteredLobby;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StageChanged += OnStageChanged;

        if (relayManager == null)
            relayManager = FindFirstObjectByType<RelayHostJoinManager>();

        networkManager = NetworkManager.Singleton;

        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
        else
        {
            Debug.LogError("NetworkManager.Singleton not found!");
        }

        if (relayManager == null)
            Debug.LogError("RelayHostJoinManager not found!");

        if (joinFoundGameButton != null)
            joinFoundGameButton.SetActive(true);

        if (joinCodeDisplayText != null)
            joinCodeDisplayText.text = "";

        if (discoveryStatusText != null)
            discoveryStatusText.text = "Host a game or enter a join code.";

        relayManager.JoinCodeReady += OnRelayJoinCodeReady;
    }

    private void OnDestroy()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        if (GameManager.Instance != null)
            GameManager.Instance.StageChanged -= OnStageChanged;

        if (Instance == this)
            Instance = null;

        if (relayManager != null)
            relayManager.JoinCodeReady -= OnRelayJoinCodeReady;
    }

    private void OnStageChanged(GameStage oldStage, GameStage newStage)
    {
        if (oldStage == GameStage.PLAY && newStage == GameStage.JOIN)
        {
            scoreboard.SetActive(true);
            HideHud();
        }
    }

    public void onPlayAgain()
    {
         scoreboard.SetActive(false);
         ShowHud();
    }

    public void onLeave()
    {
        scoreboard.SetActive(false);
        OnLeavePressed();
    }

    public void OnHostPressed()
    {
        if (relayManager == null)
        {
            SetStatus("Relay manager not found.");
            return;
        }

        connectionAttempted = true;
        enteredLobby = false;

        SetStatus("Creating Relay session...");

        if (joinCodeDisplayText != null)
            joinCodeDisplayText.text = "Creating join code...";

        relayManager.Host();
    }

    // Optional: wire this to the button that previously searched for games.
    public void OnFindGamesPressed()
    {
        SetStatus("Enter the host's Relay join code.");
    }

    // Retained name so an existing button can keep its OnClick binding.
    public void OnJoinFoundGamePressed()
    {
        if (relayManager == null)
        {
            SetStatus("Relay manager not found.");
            return;
        }

        if (joinCodeInput == null)
        {
            SetStatus("Join code input field is not assigned.");
            return;
        }

        string joinCode = joinCodeInput.text.Trim();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            SetStatus("Please enter a join code.");
            return;
        }

        connectionAttempted = true;
        enteredLobby = false;

        SetStatus("Joining Relay session...");

        relayManager.Join(joinCode);
    }

    // Called by RelayHostJoinManager when the host's join code is ready.
    public void OnRelayJoinCodeReady(string joinCode)
    {
        if (joinCodeDisplayText != null)
            joinCodeDisplayText.text = "Join code: " + joinCode;

        SetStatus("Relay created. Share the join code with the other player.");
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager == null ||
            clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (!connectionAttempted || enteredLobby)
            return;

        enteredLobby = true;

        SetStatus("Connected!");

        CloseMainMenu();
        ShowHud();

        if (GameManager.Instance != null)
            GameManager.Instance.EnterLobby();
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager == null ||
            clientId != networkManager.LocalClientId)
        {
            return;
        }

        if (connectionAttempted && !enteredLobby)
        {
            SetStatus("Connection failed or disconnected. Check the join code and try again.");
        }
        else if (enteredLobby)
        {
            SetStatus("Disconnected from the session.");
        }

        connectionAttempted = false;
        enteredLobby = false;
    }

    private void SetStatus(string message)
    {
        Debug.Log("[MenuManager] " + message);

        if (discoveryStatusText != null)
            discoveryStatusText.text = message;
    }

    public void OnStartRoundPressed()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StartSession();
    }

    public void OnLeavePressed()
    {
        connectionAttempted = false;
        enteredLobby = false;

        if (GameManager.Instance != null)
            GameManager.Instance.LeaveGame();
        else if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();

        OpenMainMenu();
        HideHud();
    }

    public void OnResumeGamePressed()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.ResumeGame();

        ClosePauseMenu();
    }

    public void ShowHud()
    {
        if (hud != null)
            hud.SetActive(true);
    }

    public void HideHud()
    {
        if (hud != null)
            hud.SetActive(false);
    }

    public void OpenMainMenu()
    {
        Debug.Log("Open Main Menu");

        OpenLobbyMenu();
        EnableMenu();
        HideHud();

        SetStatus("Host a game or enter a join code.");
    }

    public void CloseMainMenu()
    {
        Debug.Log("Close Main Menu");
        DisableMenu();
    }

    public void OpenPauseMenu()
    {
        EnableMenu();

        if (pauseMenu != null)
            pauseMenu.SetActive(true);

        if (lobbyMenu != null)
            lobbyMenu.SetActive(false);

        HideHud();

        SetPlayerControlsBlocked(true);
    }

    public void ClosePauseMenu()
    {
        DisableMenu();

        if (pauseMenu != null)
            pauseMenu.SetActive(false);

        ShowHud();

        SetPlayerControlsBlocked(false);
    }

    public void DisableMenu()
    {
        if (menuCam != null)
            menuCam.SetActive(false);

        if (canvas != null)
            canvas.SetActive(false);

        if (GameManager.Instance != null &&
            GameManager.Instance.localPlayer != null)
        {
            GameManager.Instance.localPlayer.SetEnablePlayerCamera(true);
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void EnableMenu()
    {
        if (menuCam != null)
            menuCam.SetActive(true);

        if (canvas != null)
            canvas.SetActive(true);

        if (GameManager.Instance != null &&
            GameManager.Instance.localPlayer != null)
        {
            GameManager.Instance.localPlayer.SetEnablePlayerCamera(false);
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenLobbyMenu()
    {
        if (lobbyMenu != null)
            lobbyMenu.SetActive(true);

        if (pauseMenu != null)
            pauseMenu.SetActive(false);
    }

    private void SetPlayerControlsBlocked(bool blocked)
    {
        var player = GameManager.Instance != null ? GameManager.Instance.localPlayer : null;
        if (player != null)
            player.SetControlsBlocked(blocked);
    }
}
