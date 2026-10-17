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

        playerCamera.enabled = IsOwner;
        playerAudioListener.enabled = IsOwner;
        playerMovement.EnableMovement(IsOwner);
        playerMovement.EnablePlayer(IsOwner);


        // Disable other stuff from the other player on this client
        if (localOnlyBehaviours != null)
        {
            foreach (Behaviour behaviour in localOnlyBehaviours)
            {
                if (behaviour != null && behaviour != this)
                    behaviour.enabled = IsOwner;
            }
        }
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
}
