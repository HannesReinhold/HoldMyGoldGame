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
    [SerializeField] private float lookDownLower = 0.10f;     // Meter nach unten bei vollem Runterschauen
    [SerializeField] private float lookDownForward = 0.15f;   // Meter nach vorn
    [SerializeField] private float maxLookDownAngle = 90f;
    [SerializeField] private AnimationCurve downCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

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
        //Restore(spineState);
        //Restore(headState);

        // 3) Neigung anwenden
        Apply(spineState, pitch * spineWeight, axis);
        Apply(headState, pitch * headWeight, axis);

        //ApplyPitch(spineState, pitch * spineWeight);
        //ApplyPitch(headState, pitch * headWeight);

        // 4) Kamera
        if (IsOwner && cam != null && camBaseCached)
        {
            Vector3 offset;

            if (shownPitch >= 0f)
            {
                // Runterschauen: Kopf lehnt sich nach vorn und unten
                float k = downCurve.Evaluate(Mathf.Clamp01(shownPitch / maxLookDownAngle));
                offset = new Vector3(0f, -lookDownLower * k, lookDownForward * k);
            }
            else
            {
                // Hochschauen: anheben
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

    private void ApplyPitch(BoneState b, float angle)
    {
        if (b == null || b.t == null) return;

        // Aktuelle Rotation als Basis verwenden
        Quaternion animatedLocalRotation = b.t.localRotation;

        // Pitch relativ zur lokalen Rotation hinzufügen
        b.t.localRotation =
            animatedLocalRotation *
            Quaternion.Euler(angle, 0f, 0f);
    }
}