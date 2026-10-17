using UnityEngine;

public class MenuManager : MonoBehaviour
{

    public GameObject menuCam;
    public GameObject canvas;

    public GameObject lobbyMenu;
    public GameObject pauseMenu;

    public GameObject hud;


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
        ShowHud();
        ServerManager.Instance.Host();
    }

    public void OnJoinPressed()
    {
        CloseMainMenu();
        ShowHud();
        ServerManager.Instance.Join();
    }

    public void OnLeavePressed()
    {
        CloseMainMenu();
        HideHud();
        GameManager.Instance.LeaveGame();
    }

    public void OnResumeGamePressed()
    {
        GameManager.Instance.ResumeGame();
        ShowHud();
    }

    public void ShowHud()
    {
        hud.SetActive(true);
    }

    public void HideHud()
    {
        hud.SetActive(false);
    }

    public void OpenMainMenu()
    {
        Debug.Log("Open Main Menu");
        OpenLobbyMenu();
        EnableMenu();
        HideHud();
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
        HideHud();
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
