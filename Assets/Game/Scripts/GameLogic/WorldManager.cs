using System;
using Game.Scripts.GameLogic;
using Unity.Netcode;
using UnityEngine;

public class WorldManager
{
    
    public static WorldManager Instance { get; private set; }

    
    private long passedTime;
    
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
        GameManager.Instance.StageChanged -= OnStageChanged;
    }

    
    public void Update()
    {
        switch (GameManager.Instance.stage.Value)
        {
            case GameStage.COUNTDOWN:
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GameManager.Instance.startTime.Value > GameManager.Instance.countdownSeconds * 1_000)
                {
                    GameManager.Instance.SetStage(GameStage.PLAY);
                } 
                
                break;
            case GameStage.PLAY:
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GameManager.Instance.startTime.Value > GameManager.Instance.playSeconds * 1_000 + GameManager.Instance.countdownSeconds * 1_000)
                {
                    GameManager.Instance.SetStage(GameStage.JOIN);
                }
                break;
        }
    }
    
    private void OnStageChanged(GameStage old, GameStage next)
    {
        bool isHost = GameManager.Instance.localPlayer.IsHost;
        if (next == GameStage.COUNTDOWN && old != GameStage.PAUSED && isHost)
        {
            setStartTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        } else if (next == GameStage.PAUSED && isHost)
        {
            setStartTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GameManager.Instance.startTime.Value);
        } else if (old == GameStage.PAUSED && isHost)
        {
            setStartTime(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - passedTime);
        }
    }

    private void setStartTime(long time)
    {
        GameManager.Instance.startTime.Value = time;
    }
    
}
