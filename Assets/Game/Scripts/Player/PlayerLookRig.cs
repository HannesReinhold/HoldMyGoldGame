using Unity.Netcode;
using UnityEngine;

public class PlayerLookRig : NetworkBehaviour
{
    [Header("Referenzen")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Camera cam;
    [SerializeField] private Transform spine;   // z. B. Spine oder Chest
    [SerializeField] private Transform head;

    [Header("Verteilung des Pitch")]
    [Range(0f, 1f)] [SerializeField] private float spineWeight = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float headWeight = 0.6f;
    [SerializeField] private float maxBonePitch = 70f;   // Knochen sollen sich nicht überdrehen

    [Header("Kamera")]
    [SerializeField] private bool cameraFollowsHead = true;
    [SerializeField] private Vector3 cameraOffset = new Vector3(0f, 0.05f, 0.1f);   // im Raum des Spielers

    // Owner schreibt, alle anderen lesen
    private readonly NetworkVariable<float> netPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float shownPitch;

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            shownPitch = movement.Pitch;

            // nicht jede Mini-Änderung senden
            if (Mathf.Abs(netPitch.Value - shownPitch) > 0.5f)
                netPitch.Value = shownPitch;
        }
        else
        {
            shownPitch = Mathf.Lerp(shownPitch, netPitch.Value, 15f * Time.deltaTime);
        }
    }

    // Nach dem Animator, sonst überschreibt die Animation die Rotation
    private void LateUpdate()
    {
        if (!IsSpawned) return;

        float pitch = Mathf.Clamp(shownPitch, -maxBonePitch, maxBonePitch);
        Vector3 axis = transform.right;   // Pitch immer um die Querachse des Spielers

        if (spine != null)
            spine.rotation = Quaternion.AngleAxis(pitch * spineWeight, axis) * spine.rotation;

        if (head != null)
            head.rotation = Quaternion.AngleAxis(pitch * headWeight, axis) * head.rotation;

        // Kamera: Position folgt dem Kopf, Rotation bleibt wie in PlayerMovement
        if (IsOwner && cameraFollowsHead && cam != null && head != null)
            cam.transform.position = head.position + transform.TransformDirection(cameraOffset);
    }
}