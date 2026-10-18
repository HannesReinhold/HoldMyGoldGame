namespace Game.Scripts.GameLogic
{
    public enum GameStage
    {
        NONE, // main menu
        JOIN, // until start pressed
        COUNTDOWN, // 3 seconds or smth
        PLAY, // 5 minutes, -> then join stage and display summary sceen with close button or smth or just display the previous result on the hud
        PAUSED, // just freeze movement of all players in the network
        RESULT
    }
}