using System;
using Game.Scripts.GameLogic;
using TMPro;
using UnityEngine;

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
    
    [SerializeField] private bool debug = false;

    private NetworkedPlayer playerData;

    private Color _timerDefaultColor;
    private Vector3 _timerDefaultScale;
    private float _lastWarningT = -1f;
    private bool subscribed;

    private void Awake()
    {
        _timerDefaultColor = Timer.color;
        _timerDefaultScale = Timer.rectTransform.localScale;
    }

    private void Start()
    {
        if (!debug)
        {
            Stage.enabled = false;
            Health.enabled = false;
        }
    }

    private void OnEnable()
    {
        TrySubscribe();
    }

    private void OnDisable()
    {
        if (subscribed && GameManager.Instance != null)
            GameManager.Instance.StageChanged -= OnStageChanged;

        subscribed = false;
    }

    // GameManager.Instance kann beim ersten OnEnable noch fehlen, daher auch aus Update
    private void TrySubscribe()
    {
        if (subscribed || GameManager.Instance == null) return;

        GameManager.Instance.StageChanged += OnStageChanged;
        subscribed = true;

        // aktuellen Stand sofort anzeigen
        ResetTimerWarning();
        OnStageChanged(GameStage.NONE, GameManager.Instance.CurrentStage);
    }

    private void Update()
    {
        TrySubscribe();

        var gm = GameManager.Instance;
        if (gm == null) return;

        switch (gm.CurrentStage)
        {
            case GameStage.PLAY:
                {
                    float time = gm.TimeRemaining;
                    int minutes = Mathf.FloorToInt(time / 60f);
                    int seconds = Mathf.FloorToInt(time % 60f);
                    Timer.text = $"{minutes:00}:{seconds:00}";
                    ApplyTimerWarning(time);
                    break;
                }
            case GameStage.COUNTDOWN:
                CountDown.text = Mathf.CeilToInt(gm.TimeRemaining).ToString();
                break;
        }

        if (playerData == null)
            playerData = gm.localPlayer;

        if (playerData != null)
            Health.text = playerData.health.Value + " | " + playerData.timesDied;
    }

    private void OnStageChanged(GameStage old, GameStage next)
    {
        Stage.text = next.ToString();
        CountDown.enabled = next == GameStage.COUNTDOWN;
        Timer.enabled = next == GameStage.PLAY;

        if (old == GameStage.PLAY)
            ResetTimerWarning();
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