using UnityEngine;

public class MenuManager : MonoBehaviour
{

    public GameObject menuCam;
    public GameObject canvas;

    public GameObject lobbyMenu;
    public GameObject pauseMenu;


    public static MenuManager Instance { get; private set; }

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

    public void OnHostPressed()
    {
        CloseMainMenu();

        ServerManager.Instance.Host();
    }

    public void OnJoinPressed()
    {
        CloseMainMenu();

        ServerManager.Instance.Join();
    }

    public void OnLeavePressed()
    {
        CloseMainMenu();

        GameManager.Instance.LeaveGame();
    }

    public void OnResumeGamePressed()
    {
        GameManager.Instance.ResumeGame();
    }

    public void OpenMainMenu()
    {
        Debug.Log("Open Main Menu");
        OpenLobbyMenu();
        EnableMenu();
    }

    public void CloseMainMenu()
    {
        Debug.Log("Close Main Menu");
        DisableMenu();
    }

    public void OpenPauseMenu()
    {
        Debug.Log("Open Pause Menu");
        EnableMenu();
        pauseMenu.SetActive(true);
        lobbyMenu.SetActive(false);
    }

    public void ClosePauseMenu()
    {
        Debug.Log("Close Pause Menu");
        DisableMenu();
        pauseMenu.SetActive(false);
    }

    public void DisableMenu()
    {
        menuCam.SetActive(false);
        canvas.SetActive(false);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void EnableMenu()
    {
        menuCam.SetActive(true);
        canvas.SetActive(true);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }


    // Sub Menus

    public void OpenLobbyMenu()
    {
        lobbyMenu.SetActive(true);
        pauseMenu.SetActive(false);
    }
}
