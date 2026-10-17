using System;
using Game.Scripts.GameLogic;
using UnityEngine;

public class WorldManager : MonoBehaviour
{
    
    public static WorldManager Instance { get; private set; }

    [SerializeField] private float countdownSeconds = 3f;
    [SerializeField] private float playSeconds = 300f; 
    
    private long startTime; // total game time is 5:03 while movement is restricted in the first 3 seconds
    private long passedTime;
    
    private void Awake()
    {
        Instance = this;
    }
 
    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.StageChanged += OnStageChanged;
        }
    }

    
    private void OnDestroy()
    {
        GameManager.Instance.StageChanged -= OnStageChanged;
    }

    
    void Update()
    {
        switch (GameManager.Instance.stage)
        {
            case GameStage.COUNTDOWN:
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime > countdownSeconds * 1_000)
                {
                    GameManager.Instance.SetStage(GameStage.PAUSED);
                } 
                
                break;
            case GameStage.PLAY:
                if (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime - countdownSeconds * 1_000 > playSeconds * 1_000)
                {
                    GameManager.Instance.SetStage(GameStage.JOIN);
                }
                break;
        }
    }
    
    private void OnStageChanged(GameStage old, GameStage next)
    {
        if (next == GameStage.COUNTDOWN && old != GameStage.PAUSED)
        {
            startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        } else if (next == GameStage.PAUSED)
        {
            passedTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - startTime;
        } else if (old == GameStage.PAUSED)
        {
            startTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - passedTime;
        }
    }
    
}
