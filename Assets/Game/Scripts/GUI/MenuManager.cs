using UnityEngine;

public class MenuManager : MonoBehaviour
{
    public ServerDiscoveryManager discoveryManager;

    public GameObject menuCam;
    public GameObject lobbyGUI;
    public GameObject canvas;

    public void Start()
    {
        lobbyGUI.SetActive(true);
        EnableMenu();
    }

    public void OnHostPressed()
    {
        lobbyGUI.SetActive(false);
        DisableMenu();

        discoveryManager.Host();
    }

    public void OnJoinPressed()
    {
        lobbyGUI.SetActive(false);
        DisableMenu();

        discoveryManager.Join();
    }

    public void DisableMenu()
    {
        menuCam.SetActive(false);
        canvas.SetActive(false);
    }

    public void EnableMenu()
    {
        menuCam.SetActive(true);
        canvas.SetActive(true);
    }
}
