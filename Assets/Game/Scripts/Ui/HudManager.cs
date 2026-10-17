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
    
    
    [SerializeField] private float timeWarningStart = 30f;
    [SerializeField] private float timeWarningFullAt = 25f;
    [SerializeField] private float timerWarningScale = 1.4f;
    [SerializeField] private Color timeWarningColor = Color.red;
    
    private NetworkedPlayer playerData;
    
    private Color _timerDefaultColor;
    private Vector3 _timerDefaultScale;
    private bool _warningActive;
    private float _lastWarningT = -1f;

    void Start()
    {
        GameManager.Instance.StageChanged += OnStageChanged;
        playerData = GameManager.Instance.localPlayer;
        CountDown.enabled = false;
        Timer.enabled = false;
        
        _timerDefaultColor = Timer.color;
        _timerDefaultScale = Timer.rectTransform.localScale;
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
            ApplyTimerWarning(time);
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

        if (old == GameStage.PLAY)
        {
            ResetTimerWarning();
        }
    }
    
    private void ApplyTimerWarning(float timeLeft)
    {
        float t = Mathf.InverseLerp(timeWarningStart, timeWarningFullAt, timeLeft);
        if (Mathf.Approximately(t, _lastWarningT)) return;
        _lastWarningT = t;

        float eased = EaseInOutCubic(t);
        Timer.color = Color.Lerp(_timerDefaultColor, timeWarningColor, eased);
        Timer.rectTransform.localScale = _timerDefaultScale * Mathf.Lerp(1f, timerWarningScale, eased);
    }

    private void ResetTimerWarning()
    {
        _lastWarningT = -1f;
        Timer.color = _timerDefaultColor;
        Timer.rectTransform.localScale = _timerDefaultScale;
    }
    
    private static float EaseInOutCubic(float t)
    {
        return t < 0.5f
            ? 4f * t * t * t
            : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
    }
    
}