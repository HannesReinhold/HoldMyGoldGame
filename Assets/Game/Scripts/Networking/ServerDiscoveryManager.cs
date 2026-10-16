using Unity.Netcode;
using UnityEngine;

public class ServerDiscoveryManager : MonoBehaviour
{

    public void Host()
    {
        NetworkManager.Singleton.StartHost();
    }

    public void Join()
    {
        NetworkManager.Singleton.StartHost();
    }
}


