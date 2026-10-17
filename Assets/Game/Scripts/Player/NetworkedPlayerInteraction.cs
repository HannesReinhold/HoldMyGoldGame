
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
    public float highlightRange = 5f;
    public LayerMask pickupLayers = 0;
    public float dropForwardOffset = 1.5f;
    public float dropUpOffset = 0.3f;
    public LayerMask locationLayers = 0;

    [Header("Throw Charge")]
    public float maxChargeTime = 1.5f;      // maximale Haltedauer in Sekunden
    public float tapThreshold = 0.15f;      // darunter = nur fallen lassen (normalisiert 0..1)
    public float minThrowForce = 2f;        // Kraft knapp über dem Tap-Bereich
    public float throwForce = 10;
    public bool autoThrowAtMax = false;     // true = bei voller Ladung automatisch werfen

    private bool isCharging;
    private float chargeStartTime;

    // für Animation, Ladebalken usw.
    public bool IsCharging => isCharging;
    public float Charge01 => isCharging
        ? Mathf.Clamp01((Time.time - chargeStartTime) / maxChargeTime)
        : 0f;

    private InputSystem inputActions;

    public bool enableInteractions;

    private PickableItem currentSelectedItem;
    private HighlightableZone aimedZone;



    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        networkedPlayer.itemCount.OnValueChanged += OnItemCountChanged;

        if (!IsOwner)
            return;

        inputActions = new InputSystem();
        inputActions.FPSController.PickUp.performed += OnPickupItem;
        inputActions.FPSController.Drop.started += OnDropStarted;
        inputActions.FPSController.Drop.canceled += OnDropCanceled;
        inputActions.Enable();
    }

    public override void OnNetworkDespawn()
    {
        networkedPlayer.itemCount.OnValueChanged -= OnItemCountChanged;

        if (inputActions != null)
        {
            inputActions.FPSController.Drop.started -= OnDropStarted;
            inputActions.FPSController.Drop.canceled -= OnDropCanceled;
            inputActions.FPSController.PickUp.performed -= OnPickupItem;
            inputActions.Disable();
            inputActions.Dispose();
            inputActions = null;
        }

        if (aimedZone != null) { 
            aimedZone.SetHighlight(false); 
            aimedZone = null;
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

        if (!IsOwner) return;

        UpdateAimedZone();

        if (!isCharging) return;
        // Laden abbrechen, wenn Interaktionen gesperrt werden (z. B. Pause)
        if (!enableInteractions)
        {
            isCharging = false;
            return;
        }

        if (autoThrowAtMax && Time.time - chargeStartTime >= maxChargeTime)
            ReleaseThrow();
    }


        private void OnPickupItem(InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !enableInteractions)
            return;

        Debug.Log("Try to pickup item");


        if (aimedZone is DropZone dropZone)
        {
            TakeFromDropZoneServerRpc(dropZone.NetworkObject);
            return;
        }

        if (aimedZone is PickupLocation pickupLoc)
        {
            PickupFromLocationServerRpc(pickupLoc.NetworkObject);
            return;
        }

        // CHeck if item in front of player cam
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        if (Physics.Raycast(ray, out RaycastHit hit, highlightRange, pickupLayers, QueryTriggerInteraction.Ignore))
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


    private void OnDropStarted(InputAction.CallbackContext context)
    {
        if (!IsOwner || !IsSpawned || !enableInteractions) return;
        if (networkedPlayer.GoldBarCount <= 0) return;

        // In einer Dropzone wird sofort eingezahlt, nie geworfen
        if (aimedZone is DropZone zone)
        {
            DepositToDropZoneServerRpc(zone.NetworkObject);
            return;
        }

        isCharging = true;
        chargeStartTime = Time.time;
    }

    private void OnDropCanceled(InputAction.CallbackContext context)
    {
        ReleaseThrow();
    }

    private void ReleaseThrow()
    {
        if (!isCharging) return;

        float charge = Charge01;
        isCharging = false;

        if (!IsOwner || networkedPlayer.GoldBarCount <= 0) return;

        DropItemServerRpc(playerCamera.transform.forward, charge);
    }

    private HighlightableZone GetAimedZone()
    {
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

        // QueryTriggerInteraction.Collide ist nötig, weil die Zonen Trigger-Collider sind
        if (Physics.Raycast(ray, out RaycastHit hit, highlightRange, locationLayers, QueryTriggerInteraction.Collide))
        {
            var zone = hit.collider.GetComponentInParent<HighlightableZone>();
            if (zone != null && zone.IsSpawned)
                return zone;
        }
        return null;
    }

    private void UpdateAimedZone()
    {
        HighlightableZone target = (IsSpawned && enableInteractions) ? GetAimedZone() : null;
        if (target == aimedZone) return;

        if (aimedZone != null) aimedZone.SetHighlight(false);
        aimedZone = target;
        if (aimedZone != null) aimedZone.SetHighlight(true);
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

        networkedPlayer.itemCount.Value++;

        Debug.Log("Pickup Item on server");
        target.Despawn(true);
    }

    [ServerRpc]
    private void DropItemServerRpc(Vector3 throwDirection, float charge01, ServerRpcParams rpcParams = default)
    {
        if (rpcParams.Receive.SenderClientId != OwnerClientId)
            return;

        if (networkedPlayer.itemCount.Value <= 0 || itemPrefab == null)
            return;

        // Client-Werten nie trauen
        charge01 = Mathf.Clamp01(charge01);
        throwDirection = throwDirection.sqrMagnitude > 0.001f ? throwDirection.normalized : transform.forward;

        float force = 0f;
        if (charge01 >= tapThreshold)
        {
            float t = (charge01 - tapThreshold) / (1f - tapThreshold);
            force = Mathf.Lerp(minThrowForce, throwForce, t);
        }

        Vector3 spawnPosition = dropPosition != null
            ? dropPosition.position
            : transform.position + transform.forward * dropForwardOffset + Vector3.up * dropUpOffset;

        NetworkObject droppedItem = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);

        if (force > 0f && droppedItem.TryGetComponent(out Rigidbody rb))
            rb.AddForce(throwDirection * force, ForceMode.Impulse);

        droppedItem.Spawn();

        networkedPlayer.itemCount.Value--;
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

        if (Vector3.Distance(transform.position, closest) > highlightRange + 1f)
            return;

        int amount = location.TryTake();
        if (amount > 0)
            networkedPlayer.itemCount.Value += amount;
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

        if (Vector3.Distance(transform.position, closest) > highlightRange + 1f)
            return;

        int amount = networkedPlayer.itemCount.Value;
        if (amount <= 0) return;

        zone.Deposit(amount);
        networkedPlayer.itemCount.Value = 0;
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

        if (Vector3.Distance(transform.position, closest) > highlightRange + 1f)
            return;

        int amount = zone.Take();
        if (amount > 0)
            networkedPlayer.itemCount.Value += amount;
    }
}

