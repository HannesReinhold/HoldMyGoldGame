
using Unity.Collections.LowLevel.Unsafe;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

public class NetworkedPlayerInteraction : NetworkBehaviour
{
    [Header("References")]
    public Camera playerCamera;
    public Transform dropPosition;
    public NetworkObject itemPrefab;

    [Header("Interaction")]
    public float pickupRange = 3f;
    public LayerMask pickupLayers = 0;
    public float dropForwardOffset = 1.5f;
    public float dropUpOffset = 0.3f;

    public float throwForce = 10;

    private InputSystem inputActions;

    public bool enableInteractions;


    // item numbers
    private NetworkVariable<int> itemCount = new NetworkVariable<int>(
            10,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int GoldBarCount => itemCount.Value;



    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        itemCount.OnValueChanged += OnItemCountChanged;

        if (!IsOwner)
            return;

        inputActions = new InputSystem();
        inputActions.FPSController.PickUp.performed += OnPickupItem;
        inputActions.FPSController.Drop.performed += OnDropItem;
        inputActions.Enable();
    }

    public override void OnNetworkDespawn()
    {
        itemCount.OnValueChanged -= OnItemCountChanged;

        if (inputActions != null)
        {
            inputActions.FPSController.Drop.performed -= OnPickupItem;
            inputActions.FPSController.PickUp.performed -= OnPickupItem;
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }

        base.OnNetworkDespawn();
    }

    private void OnItemCountChanged(int oldValue, int newValue)
    {
        Debug.Log("Num Items: "+newValue);
        // TODO Update Inventory GUI
    }


    private void OnPickupItem(InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !enableInteractions)
            return;

        Debug.Log("Try to pickup item");

        // CHeck if item in front of player cam
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayers, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("Collision" + hit.collider.gameObject);

            PickableItem item = hit.collider.GetComponentInParent<PickableItem>();

            if (item != null && item.IsSpawned)
            {
                Debug.Log("Pickup Item");
                PickupItemServerRpc(item.NetworkObject.NetworkObjectId);
                return;
            }
        }
    }


    private void OnDropItem(InputAction.CallbackContext context)
    {
        Debug.Log("Try to drop item");

        if (GoldBarCount > 0)
        {
            Debug.Log("Drop Item");
            Vector3 throwDirection = playerCamera.transform.forward;
            DropItemServerRpc(throwDirection);
        }
    }



    [ServerRpc]
    private void PickupItemServerRpc(ulong objectId, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(objectId, out NetworkObject target))
            return;


        PickableItem item = target.GetComponent<PickableItem>();

        if (item == null || !item.IsSpawned)
            return;

        // Double check pickup dist on server
        if (Vector3.Distance(transform.position, target.transform.position) > pickupRange + 1f)
            return;

        itemCount.Value++;

        Debug.Log("Pickup Item on server");
        target.Despawn(true);
    }

    [ServerRpc]
    private void DropItemServerRpc(Vector3 throwDirection, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (itemCount.Value <= 0 || itemPrefab == null)
            return;


        Vector3 forward = transform.forward;

        Vector3 spawnPosition;

        if (dropPosition != null)
        {
            spawnPosition = dropPosition.position;
        }
        else
        {
            spawnPosition = transform.position + forward * dropForwardOffset + Vector3.up * dropUpOffset;
        }

        Quaternion spawnRotation = Quaternion.identity;

        NetworkObject droppedItem = Instantiate( itemPrefab, spawnPosition, spawnRotation);

        // apply force
        Rigidbody rb = droppedItem.GetComponent<Rigidbody>();
        if (rb != null)
        {
            Vector3 force =
                throwDirection * throwForce;

            rb.AddForce(force, ForceMode.Impulse);
        }


        droppedItem.Spawn();

        Debug.Log("Drop Item on server");

        // Only consume inventory after successful spawning.
        itemCount.Value--;
    }
}

