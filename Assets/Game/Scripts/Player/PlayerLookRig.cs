using Unity.Netcode;
using UnityEngine;

public class PlayerLookRig : NetworkBehaviour
{
    [Header("Referenzen")]
    [SerializeField] private PlayerMovement movement;
    [SerializeField] private Camera cam;                 // Kind des Spieler-Roots, NICHT von Spine/Head
    [SerializeField] private Transform spine;
    [SerializeField] private Transform head;
    [SerializeField] private Transform eyePoint;         // optional: Child vom Head, sonst wird head verwendet

    [Header("Verteilung des Pitch")]
    [Range(0f, 1f)] [SerializeField] private float spineWeight = 0.4f;
    [Range(0f, 1f)] [SerializeField] private float headWeight = 0.6f;
    [SerializeField] private float maxBonePitch = 70f;

    [Header("Kamera: Runterschauen")]
    [Range(0f, 1f)] [SerializeField] private float lookDownFollow = 1f;   // wie stark die Kamera dem Körper nach unten folgt

    [Header("Kamera: Hochschauen")]
    [SerializeField] private float lookUpLift = 0.15f;
    [SerializeField] private float lookUpForward = 0f;
    [SerializeField] private float maxLookUpAngle = 90f;
    [SerializeField] private AnimationCurve liftCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Kamera: Ruheposition")]
    [Tooltip("Verschiebung der Kamera im Raum des Spielers: x = rechts, y = hoch, z = nach vorn")]
    [SerializeField] private Vector3 cameraRestOffset = new Vector3(0f, 0f, 0.15f);

    private readonly NetworkVariable<float> netPitch = new NetworkVariable<float>(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Owner
    );

    private float shownPitch;

    private class BoneState
    {
        public Transform t;
        public Quaternion baseLocal;
        public Quaternion lastApplied;
        public bool hasApplied;
    }

    private BoneState spineState;
    private BoneState headState;

    private Vector3 camBaseLocalPos;
    private bool camBaseCached;

    private void Awake()
    {
        if (spine != null) spineState = new BoneState { t = spine, baseLocal = spine.localRotation };
        if (head != null) headState = new BoneState { t = head, baseLocal = head.localRotation };
    }

    public override void OnNetworkSpawn()
    {
        shownPitch = netPitch.Value;

        if (cam != null)
        {
            camBaseLocalPos = cam.transform.localPosition;
            camBaseCached = true;
        }
    }

    private void Update()
    {
        if (!IsSpawned) return;

        if (IsOwner)
        {
            shownPitch = movement.Pitch;

            if (Mathf.Abs(netPitch.Value - shownPitch) > 0.5f)
                netPitch.Value = shownPitch;
        }
        else
        {
            shownPitch = Mathf.Lerp(shownPitch, netPitch.Value, 15f * Time.deltaTime);
        }
    }

    private void LateUpdate()
    {
        if (!IsSpawned) return;

        if (IsOwner)
            shownPitch = movement.Pitch;

        float pitch = Mathf.Clamp(shownPitch, -maxBonePitch, maxBonePitch);
        Vector3 axis = transform.right;

        // 1) Eigene Rotation vom letzten Frame zurücknehmen
        Restore(spineState);
        Restore(headState);

        // 2) Neutrale Position des Kopfes (ohne unsere Neigung), im Raum des Spielers
        Transform anchor = eyePoint != null ? eyePoint : head;
        Vector3 neutral = anchor != null ? transform.InverseTransformPoint(anchor.position) : Vector3.zero;

        // 3) Neigung anwenden
        Apply(spineState, pitch * spineWeight, axis);
        Apply(headState, pitch * headWeight, axis);

        // 4) Kamera
        if (IsOwner && cam != null && camBaseCached)
        {
            Vector3 offset = Vector3.zero;

            if (shownPitch > 0f && anchor != null)
            {
                // Runterschauen: Bewegung des Kopfes durch die Neigung übernehmen
                Vector3 now = transform.InverseTransformPoint(anchor.position);
                offset = (now - neutral) * lookDownFollow;
            }
            else if (shownPitch < 0f)
            {
                // Hochschauen: nur anheben
                float k = liftCurve.Evaluate(Mathf.Clamp01(-shownPitch / maxLookUpAngle));
                offset = Vector3.up * (lookUpLift * k) + Vector3.forward * (lookUpForward * k);
            }

            cam.transform.localPosition = camBaseLocalPos + cameraRestOffset + offset;
        }
    }

    private void Restore(BoneState b)
    {
        if (b == null || !b.hasApplied) return;

        // Hat der Animator den Knochen nicht überschrieben, steht noch unsere Rotation da
        if (Quaternion.Angle(b.t.localRotation, b.lastApplied) < 0.01f)
            b.t.localRotation = b.baseLocal;
    }

    private void Apply(BoneState b, float angle, Vector3 axis)
    {
        if (b == null) return;

        b.t.rotation = Quaternion.AngleAxis(angle, axis) * b.t.rotation;
        b.lastApplied = b.t.localRotation;
        b.hasApplied = true;
    }
}