using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    [Tooltip("Nur nötig, wenn keine DropZone im Parent liegt")]
    [SerializeField] private int fallbackSlot;

    private DropZone zone;

    public int slot
    {
        get
        {
            if (zone == null) zone = GetComponentInParent<DropZone>();
            return zone != null ? zone.ownerSlot : fallbackSlot;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = slot == 0 ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}