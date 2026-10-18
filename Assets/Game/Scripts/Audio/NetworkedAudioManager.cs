
using Unity.Netcode;
using UnityEngine;
using FMODUnity;

public class NetworkedAudioManager : NetworkBehaviour
{
    [Header("One-Shot FMOD Events")]
    [SerializeField]
    private EventReference[] oneShotEvents;

    [Header("Continuous FMOD Event Emitters")]
    [Tooltip("Assign emitters for walking, running, machinery, etc.")]
    [SerializeField]
    private StudioEventEmitter[] loopEmitters;

    // -1 = no continuous sound.
    // Other values are indices into loopEmitters.
    private NetworkVariable<int> currentLoop =
        new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    private int activeLoop = -1;

    public override void OnNetworkSpawn()
    {
        currentLoop.OnValueChanged += OnLoopChanged;

        // Apply the state already synchronized by the server.
        ApplyLoop(currentLoop.Value);
    }

    public override void OnNetworkDespawn()
    {
        currentLoop.OnValueChanged -= OnLoopChanged;
        StopLocalLoop();
    }

    // --------------------------------------------------
    // ONE-SHOT SOUNDS
    // --------------------------------------------------

    public void PlayOneShot(int index)
    {
        if (!IsSpawned)
            return;

        if (index < 0 || index >= oneShotEvents.Length)
            return;

        if (oneShotEvents[index].IsNull)
            return;

        PlayOneShotServerRpc(index);
    }

    [ServerRpc(RequireOwnership = false)]
    private void PlayOneShotServerRpc(
        int index,
        ServerRpcParams rpcParams = default)
    {
        if (index < 0 || index >= oneShotEvents.Length)
            return;

        if (oneShotEvents[index].IsNull)
            return;

        PlayOneShotClientRpc(index);
    }

    [ClientRpc]
    private void PlayOneShotClientRpc(int index)
    {
        if (index < 0 || index >= oneShotEvents.Length)
            return;

        EventReference sound = oneShotEvents[index];

        if (sound.IsNull)
            return;

        RuntimeManager.PlayOneShot(sound, transform.position);
    }

    // --------------------------------------------------
    // CONTINUOUS SOUNDS
    // --------------------------------------------------

    public void StartLoop(int index)
    {
        if (!IsSpawned)
            return;

        if (index < 0 || index >= loopEmitters.Length)
            return;

        SetLoopServerRpc(index);
    }

    public void StopLoop()
    {
        if (!IsSpawned)
            return;

        SetLoopServerRpc(-1);
    }

    [ServerRpc(RequireOwnership = false)]
    private void SetLoopServerRpc(
        int index,
        ServerRpcParams rpcParams = default)
    {
        if (index < -1 || index >= loopEmitters.Length)
            return;

        // The server is authoritative over the shared loop state.
        currentLoop.Value = index;
    }

    private void OnLoopChanged(int previous, int next)
    {
        ApplyLoop(next);
    }

    private void ApplyLoop(int index)
    {
        if (index == activeLoop)
            return;

        StopLocalLoop();

        if (index < 0 || index >= loopEmitters.Length)
            return;

        StudioEventEmitter emitter = loopEmitters[index];

        if (emitter == null)
        {
            Debug.LogWarning(
                $"NetworkedAudio: Loop emitter {index} is not assigned.",
                this);
            return;
        }

        emitter.Play();
        activeLoop = index;
    }

    private void StopLocalLoop()
    {
        if (activeLoop >= 0 && activeLoop < loopEmitters.Length)
        {
            StudioEventEmitter emitter = loopEmitters[activeLoop];

            if (emitter != null)
                emitter.Stop();
        }

        activeLoop = -1;
    }
}
