using System;
using TMPro;
using UnityEngine;
using UnityEngine.Serialization;
using UnityEngine.UIElements;

public class HudManager : MonoBehaviour
{
    
    [SerializeField] private TextMeshProUGUI Timer;
    [SerializeField] private TextMeshProUGUI Health;
    [SerializeField] private TextMeshProUGUI CountDown;
    private WorldManager worldManager;
    private PlayerData playerData;

    void Start()
    {
        worldManager = FindObjectOfType<WorldManager>();        
    }

    void Update()
    {
        long time = DateTime.Now.TimeOfDay.Seconds;
        
        float minutes = Mathf.FloorToInt(time / 60);  
        float seconds = Mathf.FloorToInt(time % 60);
        
        Timer.text = string.Format("{0:00}:{1:00}", minutes, seconds);

        if (playerData == null) // for some reason it's null when initialized in #Start ...
        // it's solved now but whatever, can as well stay a lazy check who cares
        {
            playerData = FindFirstObjectByType<PlayerData>();
        }
        
        if (playerData != null)
        {
            Health.text = playerData.health.ToString() + " | " + playerData.timesDied;
        }
    }
}