using Unity.Netcode;
using UnityEngine;

public static class ScoreEvaluator
{
    public static void Evaluate(MenuManager manager)
    {
        if (manager == null || manager.Result == null ||
            manager.HighScore == null || manager.LowScore == null)
            return;

        var localPlayer = GameManager.Instance != null ? GameManager.Instance.localPlayer : null;
        if (localPlayer == null)
            return;

        int ownScore = 0;
        int enemyScore = 0;

        // Der eigene Slot kommt vom Server, nicht aus der Annahme "Host = Slot 0"
        int ownSlot = localPlayer.PlayerSlot;

        foreach (var z in Object.FindObjectsByType<DropZone>(FindObjectsSortMode.None))
        {
            if (z.ownerSlot == ownSlot)
                ownScore = z.StoredItems;
            else
                enemyScore = z.StoredItems;
        }

        string enemyName = ownSlot == 0 ? "Player 2" : "Player 1";

        bool solo = NetworkManager.Singleton == null ||
                    NetworkManager.Singleton.ConnectedClientsList.Count <= 1;

        if (solo)
        {
            manager.Result.text = "";
            manager.HighScore.text = "Your score: " + ownScore;
            manager.LowScore.text = "";
            return;
        }

        if (enemyScore > ownScore)
        {
            manager.Result.text = enemyName + " won!";
            manager.HighScore.text = enemyName + ": " + enemyScore;
            manager.LowScore.text = "You: " + ownScore;
        }
        else if (enemyScore == ownScore)
        {
            manager.Result.text = "Tie!";
            manager.HighScore.text = "You: " + ownScore;
            manager.LowScore.text = enemyName + ": " + enemyScore;
        }
        else
        {
            manager.Result.text = "You won!";
            manager.HighScore.text = "You: " + ownScore;
            manager.LowScore.text = enemyName + ": " + enemyScore;
        }
    }
}
