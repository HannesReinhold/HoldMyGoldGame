using UnityEngine;

public class PlayerSpawnPoint : MonoBehaviour
{
    public int slot;

    private void OnDrawGizmos()
    {
        Gizmos.color = slot == 0 ? Color.cyan : Color.red;
        Gizmos.DrawWireSphere(transform.position, 0.5f);
        Gizmos.DrawRay(transform.position, transform.forward * 1.5f);
    }
}