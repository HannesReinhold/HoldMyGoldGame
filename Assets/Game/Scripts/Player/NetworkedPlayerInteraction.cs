
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
    public NetworkedPlayer networkedPlayer;

    [Header("Interaction")]
    public float pickupRange = 3f;
    public LayerMask pickupLayers = 0;
    public float dropForwardOffset = 1.5f;
    public float dropUpOffset = 0.3f;
    public LayerMask locationLayers = 0;

    public float throwForce = 10;

    private InputSystem inputActions;

    public bool enableInteractions;

    private PickableItem currentSelectedItem;


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

    private void Update()
    {
        /*Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, pickupRange, pickupLayers, QueryTriggerInteraction.Ignore))
        {
            Debug.Log("Item in Range" + hit.collider.gameObject);

            PickableItem item = hit.collider.GetComponentInParent<PickableItem>();
            currentSelectedItem = item;
            item.SetHighlight(true);
        }
        else
        {
            if (currentSelectedItem != null)
                currentSelectedItem.SetHighlight(false);
        }*/
    }


        private void OnPickupItem(InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !enableInteractions)
            return;

        Debug.Log("Try to pickup item");

        DropZone dropZone = FindNearest<DropZone>();
        PickupLocation pickupLoc = FindNearest<PickupLocation>();

        if (dropZone != null && (pickupLoc == null || SqrDistTo(dropZone) <= SqrDistTo(pickupLoc)))
        {
            TakeFromDropZoneServerRpc(dropZone.NetworkObject);
            return;
        }

        if (pickupLoc != null)
        {
            PickupFromLocationServerRpc(pickupLoc.NetworkObject);
            return;
        }

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

            DropZone zone = FindNearest<DropZone>();
            if (zone != null)
            {
                DepositToDropZoneServerRpc(zone.NetworkObject);
                return;   // in einer Zone wird nie physisch geworfen
            }

            Vector3 throwDirection = playerCamera.transform.forward;
            DropItemServerRpc(throwDirection);
        }
    }

    private T FindNearest<T>() where T : NetworkBehaviour
    {
        Collider[] hits = Physics.OverlapSphere(
            transform.position, pickupRange, locationLayers, QueryTriggerInteraction.Collide);

        T nearest = null;
        float best = float.MaxValue;

        foreach (var h in hits)
        {
            var c = h.GetComponentInParent<T>();
            if (c == null || !c.IsSpawned) continue;

            float d = (c.transform.position - transform.position).sqrMagnitude;
            if (d < best) { best = d; nearest = c; }
        }
        return nearest;
    }

    private float SqrDistTo(Component c) =>
        (c.transform.position - transform.position).sqrMagnitude;



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

    [ServerRpc]
    private void PickupFromLocationServerRpc(NetworkObjectReference locationRef, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (!locationRef.TryGet(out NetworkObject locObj))
            return;

        var location = locObj.GetComponent<PickupLocation>();
        if (location == null)
            return;

        Collider col = locObj.GetComponent<Collider>();
        Vector3 closest = col.ClosestPoint(transform.position);

        if (Vector3.Distance(transform.position, closest) > pickupRange + 1f)
            return;

        int amount = location.TryTake();
        if (amount > 0)
            itemCount.Value += amount;
    }

    [ServerRpc]
    private void DepositToDropZoneServerRpc(NetworkObjectReference zoneRef, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!zoneRef.TryGet(out NetworkObject zoneObj)) return;

        var zone = zoneObj.GetComponent<DropZone>();
        if (zone == null) return;

        if (zone.ownerSlot != networkedPlayer.playerSlot.Value)
            return;   // beim Gegner nicht einzahlen

        Collider col = zoneObj.GetComponent<Collider>();
        Vector3 closest = col.ClosestPoint(transform.position);

        if (Vector3.Distance(transform.position, closest) > pickupRange + 1f)
            return;

        int amount = itemCount.Value;
        if (amount <= 0) return;

        zone.Deposit(amount);
        itemCount.Value = 0;
    }

    [ServerRpc]
    private void TakeFromDropZoneServerRpc(NetworkObjectReference zoneRef, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId) return;
        if (!zoneRef.TryGet(out NetworkObject zoneObj)) return;

        var zone = zoneObj.GetComponent<DropZone>();
        if (zone == null) return;

        if (!zone.allowStealing && zone.ownerSlot != networkedPlayer.playerSlot.Value)
            return;

        Collider col = zoneObj.GetComponent<Collider>();
        Vector3 closest = col.ClosestPoint(transform.position);

        if (Vector3.Distance(transform.position, closest) > pickupRange + 1f)
            return;

        int amount = zone.Take();
        if (amount > 0)
            itemCount.Value += amount;
    }
}

