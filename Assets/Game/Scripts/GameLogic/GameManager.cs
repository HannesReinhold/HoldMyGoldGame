using System;
using Game.Scripts.GameLogic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public MenuManager menuManager;

    private InputSystem inputActions;

    public NetworkedPlayer localPlayer;
    
    public event Action<GameStage, GameStage> StageChanged;

    public GameStage stage { get; private set; } = GameStage.NONE;
    private GameStage stageBeforePause = GameStage.NONE;

    public static GameManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        DontDestroyOnLoad(gameObject);

        // input
        inputActions = new InputSystem();
        inputActions.FPSController.PauseMenu.performed += OnPauseInput;
        inputActions.Enable();
    }

    private void Start()
    {
        MenuManager.Instance.OpenMainMenu();
        SetStage(GameStage.NONE);
    }

    public void SetStage(GameStage next)
    {
        if (stage == next)
        {
            return;
        }

        GameStage old = stage;
        stage = next;
        StageChanged?.Invoke(old, next);
    }
    
    public void EnterLobby() => SetStage(GameStage.JOIN);
    
    public void StartSession()
    {
        if (stage != GameStage.JOIN)
        {
            return;
        }
        
        SetStage(GameStage.COUNTDOWN);
    }

    private void OnPauseInput(InputAction.CallbackContext context)
    {
        if (stage == GameStage.PAUSED)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        if (stage == GameStage.NONE)
        {
            return;
        }

        Debug.Log("Pause Game");
        
        if (stage == GameStage.COUNTDOWN || stage == GameStage.PLAY)
        {
            stageBeforePause = stage;
            SetStage(GameStage.PAUSED);
        }

        MenuManager.Instance.OpenPauseMenu();
    }

    public void ResumeGame()
    {
        Debug.Log("Resume Game");
        MenuManager.Instance.ClosePauseMenu();

        if (stage == GameStage.PAUSED)
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
}
