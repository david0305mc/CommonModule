using UnityEngine;

public static class GameDefine
{

    public static string IntroSceneName = "01_Intro";
    public static string MainSceneName = "02_Main";
    public static string PlayerLayerName = "Player";
    public static string EnemyLayerName = "Enemy";

    public static int HeroID = 1001;
    public static int Enemy01 = 1002;
    public static int Enemy02 = 1003;
    
}

public enum CurencyType
{
    Gold,
    Gem,
    Heart,
}

public enum Team
{
    Player,
    Enemy
}
