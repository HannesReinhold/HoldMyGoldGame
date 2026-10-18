using System;
using Game.Scripts.GameLogic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public static GameManager Instance { get; private set; }

    public MenuManager menuManager;
    public NetworkedPlayer localPlayer;
    public WorldManager worldManager;

    [Header("Ablauf")]
    [SerializeField] private int requiredPlayers = 2;   // zum Testen allein auf 1 stellen
    [SerializeField] public float countdownSeconds = 3f;
    [SerializeField] public float playSeconds = 300f;

    [SerializeField] private bool allowSoloStart = true;   // f�r Release-Builds abschaltbar

    [Header("Runden-Reset")]
    [SerializeField] private string gameplaySceneName = "Gameplay";

    private bool roundResetInProgress;

    private bool soloRound;   // nur Server: aktuelle Runde wurde allein gestartet
    private int RequiredPlayers => soloRound ? 1 : requiredPlayers;

    public event Action<GameStage, GameStage> StageChanged;

    public NetworkVariable<GameStage> stage = new NetworkVariable<GameStage>(
        GameStage.NONE,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Serverzeit, zu der die aktuelle Phase endet (-1 = kein Ende)
    public NetworkVariable<double> stageEndTime = new NetworkVariable<double>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    private InputSystem inputActions;

    // Ohne Verbindung immer NONE
    public GameStage CurrentStage => IsSpawned ? stage.Value : GameStage.NONE;

    // F�r HUD und Countdown-Anzeige, auf allen Clients gleich
    public float TimeRemaining
    {
        get
        {
            if (!IsSpawned || stageEndTime.Value < 0) return 0f;
            return (float)Math.Max(0.0, stageEndTime.Value - NetworkManager.ServerTime.Time);
        }
    }

    // Im Countdown und im Ergebnis darf sich niemand bewegen
    public bool ControlsAllowedByStage =>
        CurrentStage != GameStage.COUNTDOWN && CurrentStage != GameStage.RESULT && CurrentStage != GameStage.JOIN;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        worldManager = new WorldManager();
        worldManager.Awake();

        DontDestroyOnLoad(gameObject);

        inputActions = new InputSystem();
        inputActions.FPSController.PauseMenu.performed += OnPauseInput;
        inputActions.FPSController.StartGame.performed += OnGameStartInput;
        inputActions.Enable();
    }

    private void OnDestroy()
    {
        if (inputActions == null) return;

        inputActions.FPSController.PauseMenu.performed -= OnPauseInput;
        inputActions.FPSController.StartGame.performed -= OnGameStartInput;
        inputActions.Disable();
        inputActions.Dispose();
        inputActions = null;
    }

    private void Start()
    {
        MenuManager.Instance.OpenMainMenu();
        worldManager.Start();
    }

    public override void OnNetworkSpawn()
    {
        stage.OnValueChanged += HandleStageChanged;

        if (IsServer)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted
            += HandleLoadEventCompleted;
            SetStage(GameStage.JOIN);                    // Host ist in der Lobby
        }
        else
            HandleStageChanged(GameStage.NONE, stage.Value);   // Sp�tbeitritt: aktuellen Stand �bernehmen
    }

    public override void OnNetworkDespawn()
    {
        stage.OnValueChanged -= HandleStageChanged;

        if (NetworkManager != null && NetworkManager.SceneManager != null)
        {
            NetworkManager.SceneManager.OnLoadEventCompleted
                -= HandleLoadEventCompleted;
        }

        StageChanged?.Invoke(stage.Value, GameStage.NONE);
    }

    private void HandleStageChanged(GameStage oldStage, GameStage newStage)
    {
        Debug.Log($"Stage: {oldStage} -> {newStage}");
        StageChanged?.Invoke(oldStage, newStage);
        ApplyStageToLocalPlayer();
        ApplyStageCamera(newStage);
    }

    public void ApplyStageToLocalPlayer()
    {
        if (localPlayer != null)
            localPlayer.SetControlsBlocked(!ControlsAllowedByStage);
        localPlayer.playerMovement.SetEnableMovement(ControlsAllowedByStage);
    }

    // ---------- Server-Logik ----------

    private void Update()
    {
        worldManager.Update();

        if (!IsServer || !IsSpawned) return;

        double now = NetworkManager.ServerTime.Time;
        bool enough = ReadyPlayerCount() >= RequiredPlayers;

        switch (stage.Value)
        {
            case GameStage.JOIN:
                if (enough)
                    SetStage(GameStage.COUNTDOWN);
                break;

            case GameStage.COUNTDOWN:
                if (!enough)
                    SetStage(GameStage.JOIN);            // jemand ist wieder gegangen
                else if (now >= stageEndTime.Value)
                    SetStage(GameStage.PLAY);
                break;

            case GameStage.PLAY:
                if (!enough || now >= stageEndTime.Value)
                    SetStage(GameStage.RESULT);          // Zeit um oder Gegner weg
                break;
            
        }
    }

    private void SetStage(GameStage next)
    {
        if (!IsServer || stage.Value == next) return;

        if (next == GameStage.JOIN)
            soloRound = false;

        double now = NetworkManager.ServerTime.Time;

        // Zeit zuerst setzen, beide Werte kommen im selben Netzwerk-Tick an
        switch (next)
        {
            case GameStage.COUNTDOWN: stageEndTime.Value = now + countdownSeconds; break;
            case GameStage.PLAY: stageEndTime.Value = now + playSeconds; break;
            case GameStage.RESULT: stageEndTime.Value = -1; break;
            default: stageEndTime.Value = -1; break;
        }

        stage.Value = next;

        if (next == GameStage.COUNTDOWN)
            ResetPlayersForRound();
    }

    // Z�hlt nur Spieler, deren Objekt schon gespawnt ist
    private int ReadyPlayerCount()
    {
        int count = 0;
        foreach (var client in NetworkManager.ConnectedClientsList)
            if (client.PlayerObject != null)
                count++;
        return count;
    }

    public void PlayAgain()
    {
        if (!IsServer || stage.Value != GameStage.RESULT) return;

        // SetStage(GameStage.JOIN);
        BeginRoundReset();  
    }

    private void ResetPlayersForRound()
    {
        foreach (var client in NetworkManager.ConnectedClientsList)
        {
            if (client.PlayerObject != null &&
                client.PlayerObject.TryGetComponent(out NetworkedPlayer player))
                player.ResetForRound();
        }
    }

    // Manueller Start f�r den Host (z. B. zum Testen allein)
    public void StartSession()
    {
        if (!IsServer || stage.Value != GameStage.JOIN) return;

        // Zu wenig Spieler f�r einen normalen Start: P startet trotzdem, aber nur wenn erlaubt
        if (ReadyPlayerCount() < requiredPlayers)
        {
            if (!allowSoloStart) return;
            soloRound = true;
        }

        SetStage(GameStage.COUNTDOWN);
    }

    // ---------- Eingaben und Men� ----------

    private void OnGameStartInput(InputAction.CallbackContext context) => StartSession();

    private void OnPauseInput(InputAction.CallbackContext context)
    {
        if (CurrentStage == GameStage.NONE) return;

        // Pausemen� ist nur lokal, das Spiel l�uft f�r alle weiter
        if (MenuManager.Instance.IsPauseMenuOpen) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        if (CurrentStage == GameStage.NONE) return;
        MenuManager.Instance.OpenPauseMenu();
    }

    public void ResumeGame()
    {
        MenuManager.Instance.ClosePauseMenu();
    }

    public void LeaveGame()
    {
        Debug.Log("Leave Game...");
        ServerDiscoveryManager.Instance.Disconnect();

        SceneManager.LoadScene("NewNetworkingTEst");
        MenuManager.Instance.OpenMainMenu();
    }

    private void BeginRoundReset()
    {
        if (!IsServer || roundResetInProgress)
            return;

        roundResetInProgress = true;

        // W�hrend des Ladens darf kein Countdown beginnen.
        stageEndTime.Value = -1;

        if (NetworkManager.SceneManager == null)
        {
            Debug.LogError("NetworkSceneManager ist nicht verf�gbar.");
            roundResetInProgress = false;
            return;
        }

        var status = NetworkManager.SceneManager.LoadScene(
            gameplaySceneName,
            LoadSceneMode.Single
        );

        if (status != SceneEventProgressStatus.Started)
        {
            Debug.LogError(
                $"Gameplay-Szene konnte nicht geladen werden: {status}"
            );

            roundResetInProgress = false;
        }
    }

    private void HandleLoadEventCompleted(
        string sceneName,
        LoadSceneMode loadSceneMode,
        System.Collections.Generic.List<ulong> clientsCompleted,
        System.Collections.Generic.List<ulong> clientsTimedOut)
    {
        if (!IsServer || !roundResetInProgress)
            return;

        if (sceneName != gameplaySceneName ||
            loadSceneMode != LoadSceneMode.Single)
            return;

        if (clientsTimedOut.Count > 0)
        {
            Debug.LogWarning(
                $"Szenenwechsel: {clientsTimedOut.Count} Client(s) haben " +
                "den Ladevorgang nicht rechtzeitig abgeschlossen."
            );
        }

        // Alle Spieler, deren PlayerObject noch existiert, zur�cksetzen.
        ResetPlayersForRound();

        roundResetInProgress = false;
        soloRound = false;

        // Update() startet den Countdown automatisch, wenn genug
        // PlayerObjects vorhanden sind.
        SetStage(GameStage.JOIN);
    }

    private void ApplyStageCamera(GameStage currentStage)
    {
        if (MenuManager.Instance == null)
            return;

        switch (currentStage)
        {
            case GameStage.JOIN:
            case GameStage.RESULT:
                MenuManager.Instance.setMenuCam(true);
                break;

            case GameStage.COUNTDOWN:
            case GameStage.PLAY:
                MenuManager.Instance.setMenuCam(false);
                break;

            default:
                // NONE: Men�verhalten nach deinem Hauptmen�-Konzept
                break;
        }
    }
}