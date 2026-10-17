using System;
using Game.Scripts.GameLogic;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class HudManager : MonoBehaviour
{
    
    [SerializeField] private TextMeshProUGUI Timer;
    [SerializeField] private TextMeshProUGUI Health;
    [SerializeField] private TextMeshProUGUI CountDown;
    [SerializeField] private TextMeshProUGUI Stage;
    private NetworkedPlayer playerData;

    void Start()
    {
        GameManager.Instance.StageChanged += OnStageChanged;
        playerData = GameManager.Instance.localPlayer;
        CountDown.enabled = false;
        Timer.enabled = false;
    }

    void Update()
    {
        if (GameManager.Instance.stage.Value == GameStage.PLAY)
        {
            var gm = GameManager.Instance;

            double elapsed = (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - gm.startTime.Value) / 1000.0;
            float sinceCountdownEnded = Mathf.Max((float)(elapsed - gm.countdownSeconds), 0f);
            float time = Mathf.Max(gm.playSeconds - sinceCountdownEnded, 0f);

            int minutes = Mathf.FloorToInt(time / 60f);
            int seconds = Mathf.FloorToInt(time % 60f);
            Timer.text = $"{minutes:00}:{seconds:00}";
        }

        if (GameManager.Instance.stage.Value == GameStage.COUNTDOWN)
        {
            float time =
                (float) (DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - GameManager.Instance.startTime.Value) /
                         1_000;
            
            Debug.Log("time " + time);
            
            CountDown.text = "" + (2 - (int)Math.Floor(Math.Clamp(time, 0, GameManager.Instance.countdownSeconds)));
        }
        
        if (playerData != null)
        {
            Health.text = playerData.health.Value.ToString() + " | " + playerData.timesDied;
        }
    }
    
    private void OnStageChanged(GameStage old, GameStage next)
    {
        Stage.text = next.ToString();
        CountDown.enabled = next == GameStage.COUNTDOWN;
        Timer.enabled = next == GameStage.PLAY;
    }
}