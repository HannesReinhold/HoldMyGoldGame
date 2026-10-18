using Game.Scripts.GameLogic;
using UnityEngine;

public class WorldManager
{
    public static WorldManager Instance { get; private set; }

    public void Awake()
    {
        Instance = this;
    }

    public void Start()
    {
        GameManager.Instance.StageChanged += OnStageChanged;
    }

    public void OnDestroy()
    {
        if (GameManager.Instance != null)
            GameManager.Instance.StageChanged -= OnStageChanged;
    }

    public void Update()
    {
        // Phasenwechsel macht der GameManager auf dem Server
    }

    private void OnStageChanged(GameStage old, GameStage next)
    {
        // Hier nur noch Reaktionen der Welt auf eine neue Phase
        if (next == GameStage.COUNTDOWN && GameManager.Instance.IsServer)
            ResetWorldForRound();
    }

    // Nur Server: Welt für eine neue Runde zurücksetzen
    private void ResetWorldForRound()
    {
        foreach (var zone in Object.FindObjectsByType<DropZone>(FindObjectsSortMode.None))
            zone.ResetStored();

        // Gedroppte und geworfene Items wegräumen, in der Szene platzierte bleiben stehen
        foreach (var item in Object.FindObjectsByType<PickableItem>(FindObjectsSortMode.None))
        {
            if (item.IsSpawned && item.NetworkObject.IsSceneObject != true)
                item.NetworkObject.Despawn(true);
        }
    }
}