
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

    [Header("Drop (kurzer Tap)")]
    [Tooltip("Mehrere Punkte links/vorn/rechts. Gewählt wird der mit dem kürzesten Weg bis zum Boden.")]
    public Transform[] tapDropPoints;
    public float dropGroundCheckDistance = 3f;
    public float maxDropHeightDiff = 1f;
    public LayerMask dropSurfaceLayers = ~0;   // nur Welt, NICHT Player/PlayerHitbox

    [Header("Throw Trajectory")]
    [Tooltip("Grundwinkel in Grad. Negativ = nach unten")]
    public float throwBasePitch = -5f;
    [Tooltip("0 = Kamerawinkel ignorieren, 1 = voll übernehmen")]
    [Range(0f, 1f)] public float aimInfluence = 0.5f;
    public float minThrowPitch = -25f;   // steilster Winkel nach unten
    public float maxThrowPitch = 0f;     // 0 = nie nach oben, positive Werte erlauben einen Bogen

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
                networkedPlayer.PlayPickupSound();
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
            networkedPlayer.PlayDepositSound();
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
        networkedPlayer.PlayThrowSound();
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
        throwDirection = ComputeThrowDirection(throwDirection);

        float force = 0f;
        if (charge01 >= tapThreshold)
        {
            float t = (charge01 - tapThreshold) / (1f - tapThreshold);
            force = Mathf.Lerp(minThrowForce, throwForce, t);
        }

        bool isTap = force <= 0f;

        Vector3 spawnPosition;
        if (isTap)
        {
            if (!TryGetTapDropPosition(out spawnPosition))
                return;
        }
        else
        {
            spawnPosition = dropPosition != null
                ? dropPosition.position
                : transform.position + transform.forward * dropForwardOffset + Vector3.up * dropUpOffset;
        }

        if (!isTap)
        {
            // Spawnpunkt nicht hinter einer Wand
            Vector3 origin = transform.position + Vector3.up * 1f;
            if (Physics.Linecast(origin, spawnPosition, out RaycastHit wall,
                    dropSurfaceLayers, QueryTriggerInteraction.Ignore))
                spawnPosition = wall.point + wall.normal * 0.2f;
        }

        NetworkObject droppedItem = Instantiate(itemPrefab, spawnPosition, Quaternion.identity);

        if (!isTap)
        {
            if (droppedItem.TryGetComponent(out Rigidbody rb))
                rb.AddForce(throwDirection * force, ForceMode.Impulse);

            // Nur geworfene Items sind gefährlich
            if (droppedItem.TryGetComponent(out ItemHitDamage hit))
                hit.Arm(OwnerClientId);
        }else
        {
            if (droppedItem.TryGetComponent(out ItemHitDamage hit))
                hit.throwerClientId = OwnerClientId;
        }

        droppedItem.Spawn();
        networkedPlayer.itemCount.Value--; ;
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
        networkedPlayer.PlayPickupSound();
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

    private bool TryGetTapDropPosition(out Vector3 position)
    {
        position = default;
        float bestDistance = float.MaxValue;
        bool found = false;

        if (tapDropPoints == null) return false;

        foreach (Transform p in tapDropPoints)
        {
            if (p == null) continue;

            if (TryEvaluateDropPoint(p.position, out float distToGround) && distToGround < bestDistance)
            {
                bestDistance = distToGround;   // bei Gleichstand gewinnt der frühere Eintrag der Liste
                position = p.position;
                found = true;
            }
        }
        return found;
    }

    private bool TryEvaluateDropPoint(Vector3 p, out float distToGround)
    {
        distToGround = 0f;

        // Wand zwischen Spieler und Punkt?
        Vector3 chest = transform.position + Vector3.up * 0.8f;
        if (Physics.Linecast(chest, p, dropSurfaceLayers, QueryTriggerInteraction.Ignore))
            return false;

        // Boden darunter? Sonst Abgrund oder Mapende
        if (!Physics.Raycast(p, Vector3.down, out RaycastHit hit, dropGroundCheckDistance,
                dropSurfaceLayers, QueryTriggerInteraction.Ignore))
            return false;

        if (Mathf.Abs(hit.point.y - transform.position.y) > maxDropHeightDiff)
            return false;

        distToGround = hit.distance;
        return true;
    }

    private void OnDrawGizmosSelected()
    {
        if (tapDropPoints == null) return;
        Gizmos.color = Color.green;
        foreach (var p in tapDropPoints)
            if (p != null) Gizmos.DrawWireSphere(p.position, 0.15f);
    }

    private Vector3 ComputeThrowDirection(Vector3 aimDir)
    {
        // Seitenrichtung (Yaw) kommt aus dem Blick, die Neigung wird neu bestimmt
        Vector3 flat = new Vector3(aimDir.x, 0f, aimDir.z);
        flat = flat.sqrMagnitude > 0.001f ? flat.normalized : transform.forward;

        float aimPitchUp = Mathf.Asin(Mathf.Clamp(aimDir.normalized.y, -1f, 1f)) * Mathf.Rad2Deg;
        float pitchUp = Mathf.Clamp(throwBasePitch + aimPitchUp * aimInfluence, minThrowPitch, maxThrowPitch);

        float rad = pitchUp * Mathf.Deg2Rad;
        return flat * Mathf.Cos(rad) + Vector3.up * Mathf.Sin(rad);
    }
}

