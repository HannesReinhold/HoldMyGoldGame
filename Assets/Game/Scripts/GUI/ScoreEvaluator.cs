using TMPro;
using Unity.Netcode;
using UnityEditor;
using UnityEngine;

public static class ScoreEvaluator
{
    
    public static void Evaluate(MenuManager manager)
    {
        int ownScore = -1;
        int enemyScore = -1;
        
        var zones = Object.FindObjectsByType<DropZone>(FindObjectsSortMode.None);
        int ownSlot = GameManager.Instance.localPlayer.IsHost ? 0 : 1;
        foreach (var z in zones)
        {
            if (z.ownerSlot == ownSlot)
            {
                ownScore = z.StoredItems;
            }
            else
            {
                enemyScore = z.StoredItems;
            }
        }
        
        if (NetworkManager.Singleton.ConnectedClientsList.Count == 1)
        {
            manager.Result.text = "";
            manager.HighScore.text = "Your score: " + ownScore;
            manager.LowScore.text = "";
            return;
        }

        if (enemyScore > ownScore)
        {
            manager.Result.text = "Player 2 won!";
            manager.HighScore.text = "Player 2: " + enemyScore;
            manager.LowScore.text = "You: " + ownScore;
        }
        else if (enemyScore == ownScore)
        {
            manager.Result.text = "Tie!";
            manager.HighScore.text = "You: " + ownScore;
            manager.LowScore.text = "Player 2: " + enemyScore;
        }
        else
        {
            manager.Result.text = "You won!";
            manager.HighScore.text = "You: " + ownScore;
            manager.LowScore.text = "Player 2: " + enemyScore;
        }
    }
    
}
