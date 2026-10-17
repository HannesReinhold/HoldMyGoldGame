using UnityEngine;

public class BodyWallBlocker : MonoBehaviour
{
    [SerializeField] private CharacterController controller;
    [Tooltip("Die Collider der beiden Model-Prefabs (Schubkarre usw.)")]
    [SerializeField] private Collider[] bodyColliders;
    [Tooltip("Nur Wände/Welt, NICHT die Layer Player und PlayerHitbox")]
    [SerializeField] private LayerMask obstacleLayers;
    [SerializeField] private int iterations = 3;

    private static readonly Collider[] buffer = new Collider[16];

    public void Resolve()
    {
        for (int it = 0; it < iterations; it++)
        {
            Vector3 totalPush = Vector3.zero;

            foreach (Collider body in bodyColliders)
            {
                if (body == null || !body.enabled) continue;

                Bounds b = body.bounds;
                int n = Physics.OverlapBoxNonAlloc(
                    b.center, b.extents, buffer, Quaternion.identity,
                    obstacleLayers, QueryTriggerInteraction.Ignore);

                for (int i = 0; i < n; i++)
                {
                    Collider other = buffer[i];
                    if (other == body || other.transform.IsChildOf(transform)) continue;
                    if (other.attachedRigidbody != null) continue;   // nur statische Wände, keine Items

                    if (Physics.ComputePenetration(
                            body, body.transform.position, body.transform.rotation,
                            other, other.transform.position, other.transform.rotation,
                            out Vector3 dir, out float dist))
                    {
                        Vector3 push = dir * dist;
                        push.y = 0f;   // Boden regelt der CharacterController
                        if (push.sqrMagnitude > totalPush.sqrMagnitude)
                            totalPush = push;
                    }
                }
            }

            if (totalPush.sqrMagnitude < 0.000001f) break;
            controller.Move(totalPush);
        }
    }
}