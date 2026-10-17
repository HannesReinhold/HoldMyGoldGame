using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public MenuManager menuManager;

    private InputSystem inputActions;

    public NetworkedPlayer localPlayer;

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
        inputActions.FPSController.PauseMenu.performed += PauseGame;
        inputActions.Enable();
    }


    public void PauseGame(InputAction.CallbackContext context)
    {
        Debug.Log("Pause Game");
        MenuManager.Instance.OpenPauseMenu();
    }

    public void ResumeGame()
    {
        Debug.Log("Resume Game");
        MenuManager.Instance.ClosePauseMenu();
    }


    public void StartSession()
    {

    }

    public void LeaveGame()
    {
        Debug.Log("Leave Game...");
        ServerManager.Instance.Disconnect();

        SceneManager.LoadScene("Networking");

        MenuManager.Instance.OpenMainMenu();
    }

    private void Start()
    {
        MenuManager.Instance.OpenMainMenu();
    }
}
