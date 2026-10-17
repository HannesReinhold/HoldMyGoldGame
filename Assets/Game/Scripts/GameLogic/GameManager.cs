using System;
using Game.Scripts.GameLogic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : NetworkBehaviour
{
    public MenuManager menuManager;

    private InputSystem inputActions;

    public NetworkedPlayer localPlayer;

    public WorldManager worldManager;
    
    public event Action<GameStage, GameStage> StageChanged;

    // public GameStage stage { get; private set; } = GameStage.NONE;
    public NetworkVariable<GameStage> stage = new NetworkVariable<GameStage>(
        GameStage.NONE,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );
    
    private GameStage stageBeforePause = GameStage.NONE;
    
    [SerializeField] public float countdownSeconds = 3f;
    [SerializeField] public float playSeconds = 300f; 
    
    
    public NetworkVariable<long> startTime = new NetworkVariable<long>(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    public static GameManager Instance { get; private set; }

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

        // input
        inputActions = new InputSystem();
        inputActions.FPSController.PauseMenu.performed += OnPauseInput;
        inputActions.Enable();
        
        inputActions = new InputSystem();
        inputActions.FPSController.StartGame.performed += OnGameStartInput;
        inputActions.Enable();
    }
    
    public override void OnNetworkSpawn()
    {
        stage.OnValueChanged += (oldVal, newVal) => StageChanged?.Invoke(oldVal, newVal);

        if (IsServer)
            stage.Value = GameStage.NONE;
    }

    public override void OnNetworkDespawn()
    {
        stage.OnValueChanged = null;
    }

    private void Start()
    {
        MenuManager.Instance.OpenMainMenu();
        SetStage(GameStage.NONE);
        worldManager.Start();
    }

    public void SetStage(GameStage next)
    {
        if (stage.Value == next || !localPlayer.IsHost)
        {
            return;
        }

        GameStage old = stage.Value;
        stage.Value = next;
        StageChanged?.Invoke(old, next);
        Debug.Log("Stage changed to " + next.ToString());
    }
    
    public void EnterLobby() => SetStage(GameStage.JOIN);
    
    public void StartSession()
    {
        if (stage.Value != GameStage.JOIN)
        {
            return;
        }
        
        SetStage(GameStage.COUNTDOWN);
    }

    private void OnPauseInput(InputAction.CallbackContext context)
    {
        if (stage.Value == GameStage.PAUSED)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    private void OnGameStartInput(InputAction.CallbackContext context)
    {
        StartSession();
    }

    public void PauseGame()
    {
        if (stage.Value == GameStage.NONE)
        {
            return;
        }

        Debug.Log("Pause Game");
        
        if (stage.Value == GameStage.COUNTDOWN || stage.Value == GameStage.PLAY)
        {
            stageBeforePause = stage.Value;
            SetStage(GameStage.PAUSED);
        }

        MenuManager.Instance.OpenPauseMenu();
    }

    public void ResumeGame()
    {
        Debug.Log("Resume Game");
        MenuManager.Instance.ClosePauseMenu();

        if (stage.Value == GameStage.PAUSED)
        {
            SetStage(stageBeforePause);
        }
    }
    
    public void LeaveGame()
    {
        Debug.Log("Leave Game...");
        ServerDiscoveryManager.Instance.Disconnect();

        SetStage(GameStage.NONE);
        SceneManager.LoadScene("NewNetworkingTEst");

        MenuManager.Instance.OpenMainMenu();
    }

    private void Update()
    {
        worldManager.Update();
    }
}
