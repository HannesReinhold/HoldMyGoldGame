
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

    [Header("Server Discovery UI")]
    [SerializeField] private TMP_Text discoveryStatusText;
    [SerializeField] private GameObject joinFoundGameButton;

    public static MenuManager Instance { get; private set; }

    private ServerDiscoveryManager discoveryManager;

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
        discoveryManager = ServerDiscoveryManager.Instance;

        if (discoveryManager == null)
        {
            Debug.LogError("ServerDiscoveryManager not found!");
            return;
        }

        discoveryManager.OnHostDiscovered += OnHostDiscovered;

        if (joinFoundGameButton != null)
            joinFoundGameButton.SetActive(false);
    }

    private void OnDestroy()
    {
        if (discoveryManager != null)
            discoveryManager.OnHostDiscovered -= OnHostDiscovered;

        if (Instance == this)
            Instance = null;
    }

    public void OnHostPressed()
    {
        if (discoveryManager == null)
            return;

        discoveryManager.Host();

        CloseMainMenu();
        ShowHud();
    }

    public void OnFindGamesPressed()
    {
        if (discoveryManager == null)
            return;

        if (discoveryStatusText != null)
            discoveryStatusText.text = "Searching for games...";

        if (joinFoundGameButton != null)
            joinFoundGameButton.SetActive(false);

        discoveryManager.StartSearching();
    }

    private void OnHostDiscovered(string address)
    {
        Debug.Log($"Found host at {address}");

        if (discoveryStatusText != null)
            discoveryStatusText.text = $"Game found: {address}";

        if (joinFoundGameButton != null)
            joinFoundGameButton.SetActive(true);
    }

    public void OnJoinFoundGamePressed()
    {
        if (discoveryManager == null ||
            string.IsNullOrEmpty(discoveryManager.DiscoveredHostAddress))
        {
            if (discoveryStatusText != null)
                discoveryStatusText.text = "No game found. Search again.";

            return;
        }

        discoveryManager.JoinDiscoveredHost();

        if (discoveryStatusText != null)
            discoveryStatusText.text = "Connecting to game...";
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

        if (GameManager.Instance.localPlayer != null)
            GameManager.Instance.localPlayer.SetEnablePlayerCamera(true);

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public void EnableMenu()
    {
        menuCam.SetActive(true);
        canvas.SetActive(true);

        if (GameManager.Instance.localPlayer != null)
            GameManager.Instance.localPlayer.SetEnablePlayerCamera(false);

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void OpenLobbyMenu()
    {
        lobbyMenu.SetActive(true);
        pauseMenu.SetActive(false);
    }
}
