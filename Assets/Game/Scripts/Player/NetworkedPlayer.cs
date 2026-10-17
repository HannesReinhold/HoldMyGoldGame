using Unity.Netcode;
using UnityEngine;

public class NetworkedPlayer : NetworkBehaviour
{
    public Camera playerCamera;
    public AudioListener playerAudioListener;
    public PlayerMovement playerMovement;

    public Behaviour[] localOnlyBehaviours;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        SetEnablePlayerCamera(true);
        playerMovement.SetEnableMovement(IsOwner);
        playerMovement.SetEnablePlayer(IsOwner);


        // Disable other stuff from the other player on this client
        if (localOnlyBehaviours != null)
        {
            foreach (Behaviour behaviour in localOnlyBehaviours)
            {
                if (behaviour != null && behaviour != this)
                    behaviour.enabled = IsOwner;
            }
        }

        if (IsOwner)
            GameManager.Instance.localPlayer = this;
    }

    public override void OnNetworkDespawn()
    {
        if (IsOwner)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        base.OnNetworkDespawn();
    }

    public void SetEnablePlayerCamera(bool enabled)
    {
        playerCamera.enabled=enabled;
        playerAudioListener.enabled=enabled;
    }
}
